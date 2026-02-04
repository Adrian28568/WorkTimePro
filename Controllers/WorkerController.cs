using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkTimePro.Api.Data;
using WorkTimePro.Api.Models.Entities;

namespace WorkTimePro.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkerController : ControllerBase
    {
        private readonly AppDbContext _db;
        
        // Central European Time
        private static readonly TimeZoneInfo CET = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        
        private DateTime GetLocalTime() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CET);

        public WorkerController(AppDbContext db)
        {
            _db = db;
        }

        // ========== GET CURRENT SESSION ==========
        
        [HttpGet("current-session")]
        public async Task<IActionResult> GetCurrentSession([FromQuery] int userId)
        {
            var session = await _db.WorkSessions
                .Where(s => s.UserId == userId && !s.IsFinished)
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return Ok(new { hasSession = false });
            }

            return Ok(new
            {
                hasSession = true,
                session = new
                {
                    id = session.Id,
                    startTime = session.StartTime,
                    endTime = session.EndTime,
                    isPaused = session.IsPaused,
                    pausedMinutes = session.PausedMinutes,
                    workedMinutes = session.WorkedMinutes,
                    isFinished = session.IsFinished,
                    currentPauseStartTime = session.CurrentPauseStartTime
                }
            });
        }

        // ========== START WORK ==========
        
        [HttpPost("start")]
        public async Task<IActionResult> StartWork([FromBody] WorkerActionRequest request)
        {
            // Check if user already has an active session
            var existingSession = await _db.WorkSessions
                .Where(s => s.UserId == request.UserId && !s.IsFinished)
                .FirstOrDefaultAsync();

            if (existingSession != null)
            {
                return BadRequest(new { error = "You already have an active work session today" });
            }

            var now = GetLocalTime();

            // Create new session with LOCAL time
            var newSession = new WorkSession
            {
                UserId = request.UserId,
                StartTime = now,
                IsPaused = false,
                PausedMinutes = 0,
                IsFinished = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.WorkSessions.Add(newSession);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Work session started",
                session = new
                {
                    id = newSession.Id,
                    startTime = newSession.StartTime,
                    status = "working"
                }
            });
        }

        // ========== PAUSE WORK ==========
        
        [HttpPost("pause")]
        public async Task<IActionResult> PauseWork([FromBody] WorkerActionRequest request)
        {
            var session = await _db.WorkSessions
                .Where(s => s.UserId == request.UserId && !s.IsFinished)
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return BadRequest(new { error = "No active work session found" });
            }

            if (session.IsPaused)
            {
                return BadRequest(new { error = "You are already paused" });
            }

            var now = GetLocalTime();

            session.IsPaused = true;
            session.CurrentPauseStartTime = now;
            session.UpdatedAt = now;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Work paused",
                pausedAt = session.CurrentPauseStartTime,
                status = "paused"
            });
        }

        // ========== RESUME WORK ==========
        
        [HttpPost("resume")]
        public async Task<IActionResult> ResumeWork([FromBody] WorkerActionRequest request)
        {
            var session = await _db.WorkSessions
                .Where(s => s.UserId == request.UserId && !s.IsFinished)
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return BadRequest(new { error = "No active work session found" });
            }

            if (!session.IsPaused)
            {
                return BadRequest(new { error = "You are not currently paused" });
            }

            if (!session.CurrentPauseStartTime.HasValue)
            {
                return BadRequest(new { error = "Invalid pause state" });
            }

            var now = GetLocalTime();

            // Calculate pause duration and add to total
            var pauseDuration = (int)(now - session.CurrentPauseStartTime.Value).TotalMinutes;
            session.PausedMinutes += pauseDuration;
            
            // Clear pause state
            session.IsPaused = false;
            session.CurrentPauseStartTime = null;
            session.UpdatedAt = now;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Work resumed",
                pauseDuration = pauseDuration,
                totalPausedMinutes = session.PausedMinutes,
                status = "working"
            });
        }

        // ========== END DAY ==========
        
        [HttpPost("end")]
        public async Task<IActionResult> EndWork([FromBody] WorkerActionRequest request)
        {
            var session = await _db.WorkSessions
                .Where(s => s.UserId == request.UserId && !s.IsFinished)
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return BadRequest(new { error = "No active work session found" });
            }

            var now = GetLocalTime();

            // If currently paused, finish the pause first
            if (session.IsPaused && session.CurrentPauseStartTime.HasValue)
            {
                var pauseDuration = (int)(now - session.CurrentPauseStartTime.Value).TotalMinutes;
                session.PausedMinutes += pauseDuration;
                session.IsPaused = false;
                session.CurrentPauseStartTime = null;
            }

            session.EndTime = now;
            session.IsFinished = true;
            session.UpdatedAt = now;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Work session ended",
                endTime = session.EndTime,
                totalWorkedMinutes = session.WorkedMinutes,
                totalPausedMinutes = session.PausedMinutes,
                status = "finished"
            });
        }

        // ========== AUTO-END ON LOGOUT ==========
        
        [HttpPost("auto-end")]
        public async Task<IActionResult> AutoEndSession([FromBody] WorkerActionRequest request)
        {
            var session = await _db.WorkSessions
                .Where(s => s.UserId == request.UserId && !s.IsFinished)
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return Ok(new { message = "No active session to end" });
            }

            var now = GetLocalTime();

            // If paused, finish the pause
            if (session.IsPaused && session.CurrentPauseStartTime.HasValue)
            {
                var pauseDuration = (int)(now - session.CurrentPauseStartTime.Value).TotalMinutes;
                session.PausedMinutes += pauseDuration;
                session.IsPaused = false;
                session.CurrentPauseStartTime = null;
            }

            session.EndTime = now;
            session.IsFinished = true;
            session.UpdatedAt = now;

            // Mark user as needing approval for same-day re-login
            var user = await _db.Users.FindAsync(request.UserId);
            if (user != null)
            {
                user.NeedsReloginApproval = true;
                user.LastLogoutDate = now.Date;
            }

            await _db.SaveChangesAsync();

            return Ok(new { message = "Session auto-ended on logout" });
        }

        // ========== GET WORKER DASHBOARD DATA ==========
        
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard([FromQuery] int userId)
        {
            var user = await _db.Users
                .Include(u => u.WorkSessions)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound(new { error = "User not found" });
            }

            var now = GetLocalTime();
            var today = now.Date;
            var weekStart = today.AddDays(-(int)today.DayOfWeek + 1);
            var monthStart = new DateTime(now.Year, now.Month, 1);

            // Calculate stats
            var todayMinutes = user.WorkSessions
                .Where(s => s.StartTime.Date == today)
                .Sum(s => s.WorkedMinutes);

            var weekMinutes = user.WorkSessions
                .Where(s => s.StartTime.Date >= weekStart && s.StartTime.Date < weekStart.AddDays(7))
                .Sum(s => s.WorkedMinutes);

            var monthMinutes = user.WorkSessions
                .Where(s => s.StartTime.Date >= monthStart && s.StartTime.Date < monthStart.AddMonths(1))
                .Sum(s => s.WorkedMinutes);

            var totalSessions = user.WorkSessions.Count(s => s.IsFinished);

            var todayPaused = user.WorkSessions
                .Where(s => s.StartTime.Date == today)
                .Sum(s => s.PausedMinutes);

            // Calculate earnings
            var todayEarnings = CalculateEarnings(todayMinutes, user.HourlyRate);
            var weekEarnings = CalculateEarnings(weekMinutes, user.HourlyRate);
            var monthEarnings = CalculateEarnings(monthMinutes, user.HourlyRate);

            return Ok(new
            {
                todayMinutes,
                weekMinutes,
                monthMinutes,
                totalSessions,
                pausedMinutes = todayPaused,
                todayEarnings,
                weekEarnings,
                monthEarnings,
                needsApproval = user.NeedsReloginApproval && user.LastLogoutDate == today
            });
        }

        // ========== GET WORK HISTORY ==========
        
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions([FromQuery] int userId)
        {
            var sessions = await _db.WorkSessions
                .Where(s => s.UserId == userId && s.IsFinished)
                .OrderByDescending(s => s.StartTime)
                .Take(30)
                .Select(s => new
                {
                    id = s.Id,
                    startTime = s.StartTime,
                    endTime = s.EndTime,
                    workedMinutes = s.WorkedMinutes,
                    pausedMinutes = s.PausedMinutes
                })
                .ToListAsync();

            return Ok(sessions);
        }

        // Helper method to calculate earnings
        private decimal CalculateEarnings(int minutes, decimal hourlyRate)
        {
            decimal hours = minutes / 60m;
            
            // Add 30 min paid break if worked > 6 hours
            if (hours > 6)
            {
                hours += 0.5m;
            }
            
            return Math.Round(hours * hourlyRate, 2);
        }
    }

    public class WorkerActionRequest
    {
        public int UserId { get; set; }
    }
}