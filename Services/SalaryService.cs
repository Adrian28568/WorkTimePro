using WorkTimePro.Api.Models.Entities;

namespace WorkTimePro.Api.Services
{
    /// <summary>
    /// Professional salary calculation service
    /// Handles all European-standard time and salary computations
    /// </summary>
    public class SalaryService
    {
        /// <summary>
        /// Calculate salary for a specific work session
        /// </summary>
        public SalaryBreakdown CalculateSessionSalary(WorkSession session, AppUser user)
        {
            var workedHours = session.WorkedHours;
            var contractHours = user.ContractHoursPerDay;
            
            // Regular hours (up to contract hours)
            var regularHours = Math.Min(workedHours, contractHours);
            
            // Overtime hours (anything beyond contract hours)
            var overtimeHours = Math.Max(0, workedHours - contractHours);
            
            // Calculate amounts
            var regularAmount = regularHours * user.HourlyRate;
            var overtimeAmount = overtimeHours * user.HourlyRate * user.OvertimeMultiplier;
            var totalAmount = regularAmount + overtimeAmount;
            
            return new SalaryBreakdown
            {
                RegularHours = regularHours,
                OvertimeHours = overtimeHours,
                TotalHours = workedHours,
                RegularAmount = regularAmount,
                OvertimeAmount = overtimeAmount,
                TotalAmount = totalAmount,
                HourlyRate = user.HourlyRate,
                OvertimeRate = user.HourlyRate * user.OvertimeMultiplier
            };
        }

        /// <summary>
        /// Calculate total salary for multiple sessions (e.g., weekly or monthly)
        /// </summary>
        public SalaryBreakdown CalculateTotalSalary(IEnumerable<WorkSession> sessions, AppUser user)
        {
            var breakdown = new SalaryBreakdown
            {
                HourlyRate = user.HourlyRate,
                OvertimeRate = user.HourlyRate * user.OvertimeMultiplier
            };

            foreach (var session in sessions)
            {
                var sessionBreakdown = CalculateSessionSalary(session, user);
                
                breakdown.RegularHours += sessionBreakdown.RegularHours;
                breakdown.OvertimeHours += sessionBreakdown.OvertimeHours;
                breakdown.RegularAmount += sessionBreakdown.RegularAmount;
                breakdown.OvertimeAmount += sessionBreakdown.OvertimeAmount;
            }

            breakdown.TotalHours = breakdown.RegularHours + breakdown.OvertimeHours;
            breakdown.TotalAmount = breakdown.RegularAmount + breakdown.OvertimeAmount;

            return breakdown;
        }

        /// <summary>
        /// Calculate daily salary for a user
        /// </summary>
        public SalaryBreakdown CalculateDailySalary(AppUser user, DateTime date)
        {
            var todaySessions = user.WorkSessions
                .Where(s => s.StartTime.Date == date.Date)
                .ToList();

            return CalculateTotalSalary(todaySessions, user);
        }

        /// <summary>
        /// Calculate weekly salary for a user
        /// </summary>
        public SalaryBreakdown CalculateWeeklySalary(AppUser user, DateTime date)
        {
            var weekStart = date.Date.AddDays(-(int)date.DayOfWeek + 1); // Monday
            var weekEnd = weekStart.AddDays(7);

            var weeklySessions = user.WorkSessions
                .Where(s => s.StartTime.Date >= weekStart && s.StartTime.Date < weekEnd)
                .ToList();

            return CalculateTotalSalary(weeklySessions, user);
        }

        /// <summary>
        /// Calculate monthly salary for a user
        /// </summary>
        public SalaryBreakdown CalculateMonthlySalary(AppUser user, DateTime date)
        {
            var monthStart = new DateTime(date.Year, date.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var monthlySessions = user.WorkSessions
                .Where(s => s.StartTime.Date >= monthStart && s.StartTime.Date < monthEnd)
                .ToList();

            return CalculateTotalSalary(monthlySessions, user);
        }

        /// <summary>
        /// Get estimated monthly salary based on current pace
        /// (useful for dashboard projections)
        /// </summary>
        public decimal EstimateMonthlyEarnings(AppUser user, DateTime date)
        {
            var monthStart = new DateTime(date.Year, date.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
            var daysPassed = (date - monthStart).Days + 1;

            // Calculate what they've earned so far this month
            var currentEarnings = CalculateMonthlySalary(user, date).TotalAmount;

            // Estimate for full month
            if (daysPassed == 0) return 0;
            return (currentEarnings / daysPassed) * daysInMonth;
        }
    }

    /// <summary>
    /// Detailed salary breakdown
    /// </summary>
    public class SalaryBreakdown
    {
        public decimal RegularHours { get; set; }
        public decimal OvertimeHours { get; set; }
        public decimal TotalHours { get; set; }
        
        public decimal RegularAmount { get; set; }
        public decimal OvertimeAmount { get; set; }
        public decimal TotalAmount { get; set; }
        
        public decimal HourlyRate { get; set; }
        public decimal OvertimeRate { get; set; }
    }
}