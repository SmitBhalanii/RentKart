namespace RentKart.Core.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconClass { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public int? ParentCategoryId { get; set; }

    // Navigation properties
    public virtual Category? ParentCategory { get; set; }
    public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public virtual ICollection<Equipment> Equipments { get; set; } = new List<Equipment>();
}
