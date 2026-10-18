using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;

namespace RentKart.Infrastructure.Services;

public class WishlistService : IWishlistService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WishlistService> _logger;

    public WishlistService(ApplicationDbContext context, ILogger<WishlistService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> IsInWishlistAsync(string userId, int equipmentId)
    {
        return await _context.WishlistItems
            .AnyAsync(w => w.UserId == userId && w.EquipmentId == equipmentId);
    }

    public async Task<bool> ToggleWishlistAsync(string userId, int equipmentId)
    {
        var existing = await _context.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.EquipmentId == equipmentId);

        if (existing != null)
        {
            _context.WishlistItems.Remove(existing);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Equipment {EquipmentId} removed from wishlist for user {UserId}.", equipmentId, userId);
            return false; // Not in wishlist now
        }

        // Verify equipment exists, is active, and belongs to an approved business
        var isValid = await _context.Equipments.AnyAsync(e => 
            e.Id == equipmentId && 
            e.IsActive && 
            e.Status == EquipmentStatus.Active && 
            e.Business.Status == BusinessStatus.Approved);

        if (!isValid) return false;

        _context.WishlistItems.Add(new WishlistItem
        {
            UserId = userId,
            EquipmentId = equipmentId,
            AddedAtUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Equipment {EquipmentId} added to wishlist for user {UserId}.", equipmentId, userId);
        return true; // Now in wishlist
    }

    public async Task<bool> RemoveFromWishlistAsync(string userId, int equipmentId)
    {
        var item = await _context.WishlistItems
            .FirstOrDefaultAsync(w => w.UserId == userId && w.EquipmentId == equipmentId);

        if (item == null) return false;

        _context.WishlistItems.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<WishlistItem>> GetUserWishlistAsync(string userId)
    {
        return await _context.WishlistItems
            .AsNoTracking()
            .Include(w => w.Equipment)
                .ThenInclude(e => e.Category)
            .Include(w => w.Equipment)
                .ThenInclude(e => e.Business)
            .Include(w => w.Equipment)
                .ThenInclude(e => e.Images)
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.AddedAtUtc)
            .ToListAsync();
    }

    public async Task<HashSet<int>> GetUserWishlistEquipmentIdsAsync(string userId)
    {
        var ids = await _context.WishlistItems
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .Select(w => w.EquipmentId)
            .ToListAsync();

        return ids.ToHashSet();
    }
}
