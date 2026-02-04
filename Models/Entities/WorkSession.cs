using System;

namespace WorkTimePro.Api.Models.Entities
{
    /// <summary>
    /// Represents a single work session
    /// </summary>
    public class WorkSession
    {
        public int Id { get; set; }

        // ========== USER RELATIONSHIP ==========
        
        public int UserId { get; set; }
        public AppUser User { get; set; } = null!;

        // ========== TIME TRACKING ==========
        
        /// <summary>
        /// When the worker started (LOCAL TIME - Central European Time)
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// When the worker ended (null = still working)
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Total minutes paused (accumulated from all pause/resume cycles)
        /// </summary>
        public int PausedMinutes { get; set; } = 0;

        /// <summary>
        /// Is currently paused?
        /// </summary>
        public bool IsPaused { get; set; } = false;

        /// <summary>
        /// When current pause started (for ongoing pause calculation)
        /// </summary>
        public DateTime? CurrentPauseStartTime { get; set; }

        // ========== STATUS FLAGS ==========
        
        /// <summary>
        /// Has the worker finished this session?
        /// </summary>
        public bool IsFinished { get; set; } = false;

        // ========== COMPUTED PROPERTIES ==========
        
        /// <summary>
        /// Is this session closed?
        /// </summary>
        public bool IsClosed => EndTime != null;

        /// <summary>
        /// Total worked minutes (EXCLUDING pauses)
        /// This is the CORRECT calculation
        /// </summary>
        public int WorkedMinutes
        {
            get
            {
                // Get local time (Central European Time)
                var cet = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
                var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cet);
                
                // Calculate total duration
                var endTimeToUse = EndTime ?? now;
                var totalMinutes = (int)(endTimeToUse - StartTime).TotalMinutes;
                
                // Subtract all accumulated pauses
                var workedMinutes = totalMinutes - PausedMinutes;
                
                // If currently paused, also subtract the ongoing pause
                if (IsPaused && CurrentPauseStartTime.HasValue)
                {
                    var ongoingPauseMinutes = (int)(now - CurrentPauseStartTime.Value).TotalMinutes;
                    workedMinutes -= ongoingPauseMinutes;
                }
                
                // Never return negative
                return Math.Max(0, workedMinutes);
            }
        }

        /// <summary>
        /// Total worked hours (for display)
        /// </summary>
        public decimal WorkedHours => WorkedMinutes / 60m;

        // ========== METADATA ==========
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}