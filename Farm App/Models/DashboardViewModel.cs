namespace Farm_App.Models;

public class DashboardViewModel
{
    public int ActiveAnimals { get; set; }
    public int TotalAnimals { get; set; }
    public int BirthsThisMonth { get; set; }
    public int DeathsThisMonth { get; set; }
    public decimal RainfallThisMonth { get; set; }
    public int CampCount { get; set; }
    public List<LivestockEvent> RecentEvents { get; set; } = [];
    public List<SpeciesCount> SpeciesCounts { get; set; } = [];
}

public class SpeciesCount
{
    public string Species { get; set; } = string.Empty;
    public int Count { get; set; }
}