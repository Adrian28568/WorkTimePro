using System;
using System.Collections.Generic;

namespace WorkTimePro.Api.Models.Entities
{
    /// <summary>
    /// Represents a user in the system (Worker or Admin)
    /// </summary>
    public class AppUser
    {
        public int Id { get; set; }

        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }

        // ========== SALARY & CONTRACT INFO ==========
        
        /// <summary>
        /// Hourly rate in EUR (default €13/hour)
        /// </summary>
        public decimal HourlyRate { get; set; } = 13.00m;

        /// <summary>
        /// Standard working hours per day (typically 8 hours in Europe)
        /// </summary>
        public decimal ContractHoursPerDay { get; set; } = 8m;

        /// <summary>
        /// Overtime multiplier (e.g., 1.25 = 125% of normal rate)
        /// </summary>
        public decimal OvertimeMultiplier { get; set; } = 1.25m;

        // ========== RE-LOGIN APPROVAL SYSTEM ==========
        
        /// <summary>
        /// Does this worker need admin approval to clock in again today?
        /// Set to true when they logout during the day
        /// </summary>
        public bool NeedsReloginApproval { get; set; } = false;

        /// <summary>
        /// Date of last logout (used to check if same-day re-login)
        /// </summary>
        public DateTime? LastLogoutDate { get; set; }

        // ========== METADATA ==========
        
        /// <summary>
        /// When this user account was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Is this account active?
        /// </summary>
        public bool IsActive { get; set; } = true;

        // ========== RELATIONSHIPS ==========
        
        public ICollection<WorkSession> WorkSessions { get; set; }
            = new List<WorkSession>();
    }
}