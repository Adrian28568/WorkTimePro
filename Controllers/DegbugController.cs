using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorkTimePro.Api.Data;

namespace WorkTimePro.Api.Controllers
{
    /// <summary>
    /// Debug endpoint to see raw data - REMOVE IN PRODUCTION
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DebugController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// See raw session data to debug time calculations
        /// </summary>
        [HttpGet("session/{sessionId}")]
        public async Task<IActionResult> GetSessionDebug(int sessionId)
        {
            var session = await _db.WorkSessions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
            {
                return NotFound("Session not found");
            }

            var now = DateTime.UtcNow;
            var totalDuration = (now - session.StartTime).TotalMinutes;

            return Ok(new
            {
                // Raw values from DB
                sessionId = session.Id,
                userId = session.UserId,
                username = session.User.Username,
                startTime = session.StartTime,
                endTime = session.EndTime,
                pausedMinutes = session.PausedMinutes,
                isPaused = session.IsPaused,
                currentPauseStartTime = session.CurrentPauseStartTime,
                isFinished = session.IsFinished,
                
                // Time calculations
                currentTime_UTC = now,
                totalDuration_Minutes = Math.Round(totalDuration, 2),
                workedMinutes_Property = session.WorkedMinutes,
                
                // Manual calculation for comparison
                manualCalc_TotalMinutes = Math.Round((now - session.StartTime).TotalMinutes, 2),
                manualCalc_MinusPaused = Math.Round((now - session.StartTime).TotalMinutes - session.PausedMinutes, 2),
                
                // Time zone info
                serverTimeZone = TimeZoneInfo.Local.Id,
                serverLocalTime = DateTime.Now,
                
                // Breakdown
                breakdown = new
                {
                    startedAt = session.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    now = now.ToString("yyyy-MM-dd HH:mm:ss"),
                    difference = $"{Math.Floor(totalDuration / 60)}h {Math.Floor(totalDuration % 60)}m",
                    pausedSoFar = $"{session.PausedMinutes}m",
                    actualWorked = $"{session.WorkedMinutes}m"
                }
            });
        }

        /// <summary>
        /// See all sessions for a user
        /// </summary>
        [HttpGet("user/{userId}/sessions")]
        public async Task<IActionResult> GetUserSessions(int userId)
        {
            var sessions = await _db.WorkSessions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.StartTime)
                .Take(10)
                .Select(s => new
                {
                    id = s.Id,
                    startTime = s.StartTime,
                    endTime = s.EndTime,
                    pausedMinutes = s.PausedMinutes,
                    workedMinutes = s.WorkedMinutes,
                    isPaused = s.IsPaused,
                    isFinished = s.IsFinished,
                    createdAt = s.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                userId,
                sessionCount = sessions.Count,
                serverTime = DateTime.UtcNow,
                sessions
            });
        }

        /// <summary>
        /// Test time calculations
        /// </summary>
        [HttpGet("test-time")]
        public IActionResult TestTime()
        {
            var now = DateTime.UtcNow;
            var testStart = now.AddHours(-2).AddMinutes(-15); // 2h 15m ago

            return Ok(new
            {
                test = "Time calculation test",
                now = now,
                testStart = testStart,
                difference_TotalMinutes = (now - testStart).TotalMinutes,
                difference_Formatted = $"{Math.Floor((now - testStart).TotalMinutes / 60)}h {Math.Floor((now - testStart).TotalMinutes % 60)}m",
                serverTimeZone = TimeZoneInfo.Local.Id,
                utcNow = DateTime.UtcNow,
                localNow = DateTime.Now
            });
        }
    }
}