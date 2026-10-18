using RentKart.Core.Entities;

namespace RentKart.Core.Interfaces;

public interface IWishlistService
{
    Task<bool> IsInWishlistAsync(string userId, int equipmentId);
    Task<bool> ToggleWishlistAsync(string userId, int equipmentId);
    Task<bool> RemoveFromWishlistAsync(string userId, int equipmentId);
    Task<List<WishlistItem>> GetUserWishlistAsync(string userId);
    Task<HashSet<int>> GetUserWishlistEquipmentIdsAsync(string userId);
}
