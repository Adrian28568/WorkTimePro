using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkTimePro.Api.Data;

namespace WorkTimePro.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private static readonly TimeZoneInfo CET = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        
        private DateTime GetLocalTime() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CET);

        public AdminController(AppDbContext db)
        {
            _db = db;
        }

        // ========== DASHBOARD STATS ==========
        
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var now = GetLocalTime();
            var today = now.Date;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var allUsers = await _db.Users
                .Include(u => u.WorkSessions)
                .Where(u => !u.IsAdmin)
                .ToListAsync();

            var totalWorkers = allUsers.Count;
            
            // Active sessions today (not finished)
            var activeToday = allUsers.Count(u => 
                u.WorkSessions.Any(s => s.StartTime.Date == today && !s.IsFinished));
            
            // Currently working (active and not paused)
            var currentlyWorking = allUsers.Count(u => 
                u.WorkSessions.Any(s => !s.IsFinished && !s.IsPaused));
            
            // Currently paused
            var currentlyPaused = allUsers.Count(u => 
                u.WorkSessions.Any(s => !s.IsFinished && s.IsPaused));
            
            // Not clocked in today
            var notClockedIn = totalWorkers - allUsers.Count(u => 
                u.WorkSessions.Any(s => s.StartTime.Date == today));
            
            // Total minutes today
            var totalMinutesToday = allUsers
                .SelectMany(u => u.WorkSessions)
                .Where(s => s.StartTime.Date == today)
                .Sum(s => s.WorkedMinutes);

            // Calculate ACTUAL monthly payroll (all finished sessions this month)
            var monthlyPayroll = 0m;
            foreach (var user in allUsers)
            {
                var monthSessions = user.WorkSessions
                    .Where(s => s.StartTime >= monthStart && s.StartTime < monthStart.AddMonths(1) && s.IsFinished)
                    .ToList();
                
                var totalMinutes = monthSessions.Sum(s => s.WorkedMinutes);
                var hourlyRate = user.HourlyRate > 0 ? user.HourlyRate : 13m; // Default €13
                
                // €0.216 per minute (€13 per hour / 60 minutes)
                var minuteRate = hourlyRate / 60m;
                monthlyPayroll += totalMinutes * minuteRate;
            }

            return Ok(new
            {
                totalWorkers,
                activeToday,
                totalMinutesToday,
                currentlyWorking,
                currentlyPaused,
                notClockedIn,
                monthlyPayroll = Math.Round(monthlyPayroll, 2)
            });
        }

        // ========== WORKERS LIST ==========
        
        [HttpGet("workers")]
        public async Task<IActionResult> GetWorkers()
        {
            var now = GetLocalTime();
            var today = now.Date;
            var weekStart = today.AddDays(-(int)today.DayOfWeek + 1);
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var workers = await _db.Users
                .Include(u => u.WorkSessions)
                .Where(u => !u.IsAdmin)
                .ToListAsync();

            var result = workers.Select(u =>
            {
                var hourlyRate = u.HourlyRate > 0 ? u.HourlyRate : 13m;
                var minuteRate = hourlyRate / 60m;

                var todayMinutes = u.WorkSessions
                    .Where(s => s.StartTime.Date == today)
                    .Sum(s => s.WorkedMinutes);

                var weekMinutes = u.WorkSessions
                    .Where(s => s.StartTime.Date >= weekStart)
                    .Sum(s => s.WorkedMinutes);

                var monthMinutes = u.WorkSessions
                    .Where(s => s.StartTime.Date >= monthStart && s.IsFinished)
                    .Sum(s => s.WorkedMinutes);

                var pausedMinutes = u.WorkSessions
                    .Where(s => s.StartTime.Date == today)
                    .Sum(s => s.PausedMinutes);

                var activeSession = u.WorkSessions
                    .FirstOrDefault(s => !s.IsFinished);

                var isWorking = activeSession != null && !activeSession.IsPaused;
                var isPaused = activeSession != null && activeSession.IsPaused;

                var monthEarnings = monthMinutes * minuteRate;

                return new
                {
                    id = u.Id,
                    username = u.Username,
                    isWorking,
                    isPaused,
                    todayMinutes,
                    weekMinutes,
                    monthMinutes,
                    pausedMinutes,
                    monthEarnings = Math.Round(monthEarnings, 2),
                    lastClockIn = activeSession?.StartTime,
                    status = isWorking ? "Working" : isPaused ? "Paused" : "Idle"
                };
            }).ToList();

            return Ok(result);
        }

        // ========== WORKER DETAILS (CALENDAR VIEW) ==========
        
        [HttpGet("worker/{id}/details")]
        public async Task<IActionResult> GetWorkerDetails(int id, [FromQuery] int? month, [FromQuery] int? year)
        {
            var worker = await _db.Users
                .Include(u => u.WorkSessions)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (worker == null || worker.IsAdmin)
            {
                return NotFound(new { error = "Worker not found" });
            }

            var now = GetLocalTime();
            var targetYear = year ?? now.Year;
            var targetMonth = month ?? now.Month;

            var monthStart = new DateTime(targetYear, targetMonth, 1);
            var monthEnd = monthStart.AddMonths(1);
            var daysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);

            var hourlyRate = worker.HourlyRate > 0 ? worker.HourlyRate : 13m;
            var minuteRate = hourlyRate / 60m;

            // Get all sessions for this month
            var monthlySessions = worker.WorkSessions
                .Where(s => s.StartTime >= monthStart && s.StartTime < monthEnd)
                .ToList();

            // Build daily breakdown
            var dailyBreakdown = new List<object>();
            
            // Only show days that have passed OR today
            var lastDayToShow = now.Date >= monthEnd ? daysInMonth : now.Day;
            
            for (int day = 1; day <= lastDayToShow; day++)
            {
                var date = new DateTime(targetYear, targetMonth, day);
                var dayOfWeek = date.ToString("dddd");
                var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
                var isPastDay = date < now.Date; // Only count missed if day has passed

                var daySessions = monthlySessions.Where(s => s.StartTime.Date == date).ToList();
                
                if (daySessions.Any())
                {
                    var firstSession = daySessions.OrderBy(s => s.StartTime).First();
                    var lastSession = daySessions.OrderByDescending(s => s.EndTime ?? s.StartTime).First();
                    var totalWorked = daySessions.Sum(s => s.WorkedMinutes);
                    var totalPaused = daySessions.Sum(s => s.PausedMinutes);
                    
                    // Convert minutes to proper hours and minutes
                    var hours = totalWorked / 60;
                    var minutes = totalWorked % 60;
                    var workedDisplay = $"{hours}h {minutes}m";

                    dailyBreakdown.Add(new
                    {
                        date = date.ToString("yyyy-MM-dd"),
                        dayOfWeek,
                        startTime = firstSession.StartTime.ToString("HH:mm"),
                        endTime = lastSession.EndTime?.ToString("HH:mm") ?? "Ongoing",
                        workedMinutes = totalWorked,
                        workedDisplay,
                        pausedMinutes = totalPaused,
                        status = firstSession.IsFinished ? "Finished" : "Ongoing",
                        isLongDay = totalWorked > 600 // >10 hours
                    });
                }
                else if (!isWeekend && isPastDay)
                {
                    // Missed day (only if weekday and already passed)
                    dailyBreakdown.Add(new
                    {
                        date = date.ToString("yyyy-MM-dd"),
                        dayOfWeek,
                        startTime = (string?)null,
                        endTime = (string?)null,
                        workedMinutes = 0,
                        workedDisplay = "0h 0m",
                        pausedMinutes = 0,
                        status = "No Clock-In",
                        isLongDay = false
                    });
                }
            }

            // Calculate month summary
            var totalMonthMinutes = monthlySessions.Where(s => s.IsFinished).Sum(s => s.WorkedMinutes);
            var totalMonthHours = totalMonthMinutes / 60;
            var totalMonthMins = totalMonthMinutes % 60;
            var totalMonthEarnings = totalMonthMinutes * minuteRate;
            
            var daysWorked = dailyBreakdown.Count(d => 
            {
                var status = d.GetType().GetProperty("status")?.GetValue(d)?.ToString();
                return status != "No Clock-In";
            });
            
            var daysMissed = dailyBreakdown.Count(d => 
            {
                var status = d.GetType().GetProperty("status")?.GetValue(d)?.ToString();
                return status == "No Clock-In";
            });

            return Ok(new
            {
                workerId = worker.Id,
                username = worker.Username,
                hourlyRate,
                month = targetMonth,
                year = targetYear,
                monthSummary = new
                {
                    totalMinutes = totalMonthMinutes,
                    totalHours = $"{totalMonthHours}h {totalMonthMins}m",
                    totalEarned = Math.Round(totalMonthEarnings, 2),
                    daysWorked,
                    daysMissed
                },
                dailyBreakdown
            });
        }

        // ========== MONTHLY PAYROLL OVERVIEW ==========
        
        [HttpGet("payroll-overview")]
        public async Task<IActionResult> GetPayrollOverview([FromQuery] int? month, [FromQuery] int? year)
        {
            var now = GetLocalTime();
            var targetYear = year ?? now.Year;
            var targetMonth = month ?? now.Month;

            var monthStart = new DateTime(targetYear, targetMonth, 1);
            var monthEnd = monthStart.AddMonths(1);

            var workers = await _db.Users
                .Include(u => u.WorkSessions)
                .Where(u => !u.IsAdmin)
                .ToListAsync();

            var workerPayrolls = workers.Select(w =>
            {
                var hourlyRate = w.HourlyRate > 0 ? w.HourlyRate : 13m;
                var minuteRate = hourlyRate / 60m;

                var sessions = w.WorkSessions
                    .Where(s => s.StartTime >= monthStart && s.StartTime < monthEnd && s.IsFinished)
                    .ToList();

                var daysWorked = sessions
                    .Select(s => s.StartTime.Date)
                    .Distinct()
                    .Count();

                var totalMinutes = sessions.Sum(s => s.WorkedMinutes);
                var totalHours = totalMinutes / 60;
                var totalMins = totalMinutes % 60;

                var totalEarned = totalMinutes * minuteRate;

                return new
                {
                    username = w.Username,
                    daysWorked,
                    totalMinutes,
                    totalHours = $"{totalHours}h {totalMins}m",
                    totalEarned = Math.Round(totalEarned, 2),
                    hourlyRate
                };
            }).ToList();

            var grandTotal = workerPayrolls.Sum(w => w.totalEarned);

            var monthNames = new[] { "", "January", "February", "March", "April", "May", "June", 
                                    "July", "August", "September", "October", "November", "December" };

            return Ok(new
            {
                month = targetMonth,
                year = targetYear,
                monthName = monthNames[targetMonth],
                workers = workerPayrolls,
                grandTotal = Math.Round(grandTotal, 2)
            });
        }

        // ========== APPROVE RE-LOGIN ==========
        
        [HttpPost("approve-relogin/{userId}")]
        public async Task<IActionResult> ApproveRelogin(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            
            if (user == null || user.IsAdmin)
            {
                return NotFound(new { error = "Worker not found" });
            }

            user.NeedsReloginApproval = false;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Worker approved to clock in again" });
        }
    }
}