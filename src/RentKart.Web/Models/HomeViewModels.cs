using RentKart.Web.Models.Catalog;

namespace RentKart.Web.Models;

public class HomeIndexViewModel
{
    public List<CategoryOptionViewModel> Categories { get; set; } = new();
    public List<EquipmentListItemViewModel> FeaturedListings { get; set; } = new();
    public List<string> AvailableCities { get; set; } = new();
}
