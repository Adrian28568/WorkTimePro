namespace WorkTimePro.Api.Models
{
    public class DashboardStatsDto
    {
        public int TotalWorkers { get; set; }
        public int ActiveToday { get; set; }
        public double TotalMinutesToday { get; set; }
        public int CurrentlyWorking { get; set; }
        public int CurrentlyPaused { get; set; }
    }
}
