using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Infrastructure.Identity;

namespace RentKart.Infrastructure.Data;

public static class CatalogDataSeeder
{
    public static async Task SeedCatalogDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CatalogDataSeeder");

        // 1. Seed Core Categories if missing
        var categories = new List<Category>
        {
            new() { Name = "Cameras & Optics", Slug = "cameras-optics", Description = "Professional mirrorless cameras, cinema gear, and prime lenses.", IconClass = "camera", DisplayOrder = 1, IsActive = true },
            new() { Name = "Power Tools & Construction", Slug = "power-tools", Description = "Drills, saws, rotary hammers, and building tools.", IconClass = "tool", DisplayOrder = 2, IsActive = true },
            new() { Name = "Audio Visual & Projectors", Slug = "projectors-audio-visual", Description = "High-lumen projectors, PA systems, and conference monitors.", IconClass = "film", DisplayOrder = 3, IsActive = true },
            new() { Name = "Lawn & Garden Machinery", Slug = "lawn-garden", Description = "Aerators, rototillers, leaf blowers, and pressure washers.", IconClass = "feather", DisplayOrder = 4, IsActive = true },
            new() { Name = "Camping & Expedition", Slug = "camping-outdoor", Description = "Tents, portable power stations, and backpacking equipment.", IconClass = "compass", DisplayOrder = 5, IsActive = true },
            new() { Name = "Party & Staging", Slug = "party-staging", Description = "Tables, canopy tents, lighting rigs, and PA consoles.", IconClass = "gift", DisplayOrder = 6, IsActive = true }
        };

        var addedCat = false;
        foreach (var cat in categories)
        {
            if (!await context.Categories.AnyAsync(c => c.Slug == cat.Slug))
            {
                context.Categories.Add(cat);
                addedCat = true;
            }
        }
        if (addedCat)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Ensured initial equipment categories exist.");
        }

        // 2. Seed Verified Businesses and Listings
        var catCameras = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "cameras-optics");
        var catTools = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "power-tools");
        var catAv = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "projectors-audio-visual");

        if (catCameras == null || catTools == null || catAv == null)
        {
            return;
        }

        // Create Business 1 (Approved - San Francisco)
            var store1Email = "store.sf@rentkart.local";
            var user1 = await userManager.FindByEmailAsync(store1Email);
            if (user1 == null)
            {
                user1 = new ApplicationUser
                {
                    UserName = store1Email,
                    Email = store1Email,
                    FullName = "Marcus Vance",
                    PhoneNumber = "+1 (415) 555-0142",
                    UserType = UserRole.Business,
                    EmailConfirmed = true,
                    IsActive = true
                };
                await userManager.CreateAsync(user1, "StorePass@2026!");
                await userManager.AddToRoleAsync(user1, IdentityDataSeeder.RoleBusiness);
            }

            var biz1 = await context.BusinessProfiles.FirstOrDefaultAsync(b => b.UserId == user1.Id);
            if (biz1 == null)
            {
                biz1 = new BusinessProfile
                {
                    UserId = user1.Id,
                    ShopName = "Bay City Camera & Cine Rentals",
                    RegistrationNumber = "CA-SF-88912",
                    ContactPhone = "+1 (415) 555-0142",
                    AddressLine = "742 Howard Street",
                    City = "San Francisco",
                    State = "CA",
                    PostalCode = "94103",
                    OperatingHours = "Mon-Fri: 08:30 - 18:00, Sat: 09:00 - 16:00",
                    PickupInstructions = "Pick up at counter inside Suite 100. Valid government ID required.",
                    Status = BusinessStatus.Approved,
                    ApprovedAtUtc = DateTime.UtcNow
                };
                context.BusinessProfiles.Add(biz1);
                await context.SaveChangesAsync();
            }

            // Create Business 2 (Approved - San Jose)
            var store2Email = "store.sj@rentkart.local";
            var user2 = await userManager.FindByEmailAsync(store2Email);
            if (user2 == null)
            {
                user2 = new ApplicationUser
                {
                    UserName = store2Email,
                    Email = store2Email,
                    FullName = "David Chen",
                    PhoneNumber = "+1 (408) 555-0188",
                    UserType = UserRole.Business,
                    EmailConfirmed = true,
                    IsActive = true
                };
                await userManager.CreateAsync(user2, "StorePass@2026!");
                await userManager.AddToRoleAsync(user2, IdentityDataSeeder.RoleBusiness);
            }

            var biz2 = await context.BusinessProfiles.FirstOrDefaultAsync(b => b.UserId == user2.Id);
            if (biz2 == null)
            {
                biz2 = new BusinessProfile
                {
                    UserId = user2.Id,
                    ShopName = "Apex Machinery & Pro Tool Hire",
                    RegistrationNumber = "CA-SJ-44109",
                    ContactPhone = "+1 (408) 555-0188",
                    AddressLine = "1250 S 10th Street",
                    City = "San Jose",
                    State = "CA",
                    PostalCode = "95112",
                    OperatingHours = "Mon-Sat: 07:00 - 17:30",
                    PickupInstructions = "Loading bay door 3 behind main showroom for large equipment.",
                    Status = BusinessStatus.Approved,
                    ApprovedAtUtc = DateTime.UtcNow
                };
                context.BusinessProfiles.Add(biz2);
                await context.SaveChangesAsync();
            }

            // Create Business 3 (Approved - Oakland)
            var store3Email = "store.oak@rentkart.local";
            var user3 = await userManager.FindByEmailAsync(store3Email);
            if (user3 == null)
            {
                user3 = new ApplicationUser
                {
                    UserName = store3Email,
                    Email = store3Email,
                    FullName = "Elena Rostova",
                    PhoneNumber = "+1 (510) 555-0164",
                    UserType = UserRole.Business,
                    EmailConfirmed = true,
                    IsActive = true
                };
                await userManager.CreateAsync(user3, "StorePass@2026!");
                await userManager.AddToRoleAsync(user3, IdentityDataSeeder.RoleBusiness);
            }

            var biz3 = await context.BusinessProfiles.FirstOrDefaultAsync(b => b.UserId == user3.Id);
            if (biz3 == null)
            {
                biz3 = new BusinessProfile
                {
                    UserId = user3.Id,
                    ShopName = "SoundWave Stage & AV Productions",
                    RegistrationNumber = "CA-OAK-91024",
                    ContactPhone = "+1 (510) 555-0164",
                    AddressLine = "2201 Broadway",
                    City = "Oakland",
                    State = "CA",
                    PostalCode = "94612",
                    OperatingHours = "Mon-Fri: 09:00 - 18:30",
                    PickupInstructions = "Street level parking available. Check in at audio rental desk.",
                    Status = BusinessStatus.Approved,
                    ApprovedAtUtc = DateTime.UtcNow
                };
                context.BusinessProfiles.Add(biz3);
                await context.SaveChangesAsync();
            }

            // Create Business 4 (PendingApproval - Fremont) -> To verify test filtering!
            var store4Email = "store.pending@rentkart.local";
            var user4 = await userManager.FindByEmailAsync(store4Email);
            if (user4 == null)
            {
                user4 = new ApplicationUser
                {
                    UserName = store4Email,
                    Email = store4Email,
                    FullName = "Pending Owner",
                    PhoneNumber = "+1 (510) 555-0999",
                    UserType = UserRole.Business,
                    EmailConfirmed = true,
                    IsActive = true
                };
                await userManager.CreateAsync(user4, "StorePass@2026!");
                await userManager.AddToRoleAsync(user4, IdentityDataSeeder.RoleBusiness);
            }

            var biz4 = await context.BusinessProfiles.FirstOrDefaultAsync(b => b.UserId == user4.Id);
            if (biz4 == null)
            {
                biz4 = new BusinessProfile
                {
                    UserId = user4.Id,
                    ShopName = "Unverified Fremont Rentals",
                    ContactPhone = "+1 (510) 555-0999",
                    AddressLine = "39000 Fremont Blvd",
                    City = "Fremont",
                    State = "CA",
                    PostalCode = "94538",
                    OperatingHours = "Mon-Fri: 09:00 - 17:00",
                    Status = BusinessStatus.PendingApproval
                };
                context.BusinessProfiles.Add(biz4);
                await context.SaveChangesAsync();
            }

            // Seed Active Equipment for Approved Businesses
            var listings = new List<Equipment>
            {
                // Store 1: Cameras
                new()
                {
                    BusinessId = biz1.Id,
                    CategoryId = catCameras.Id,
                    Title = "Sony Alpha a7 IV Full-Frame Hybrid Camera",
                    Description = "33MP Exmor R CMOS sensor, 4K 60p 10-bit recording, Real-time Eye AF for photo and video. Includes 2 batteries, dual charger, and 128GB V90 SD card.",
                    DailyRentalRate = 65.00m,
                    SecurityDeposit = 250.00m,
                    TotalQuantity = 3,
                    Condition = EquipmentCondition.LikeNew,
                    Status = EquipmentStatus.Active,
                    IsActive = true,
                    Images = new List<EquipmentImage>
                    {
                        new() { ImageUrl = "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?auto=format&fit=crop&w=800&q=80", IsPrimary = true, DisplayOrder = 1 }
                    }
                },
                new()
                {
                    BusinessId = biz1.Id,
                    CategoryId = catCameras.Id,
                    Title = "Canon RF 24-70mm f/2.8L IS USM Lens",
                    Description = "Standard zoom lens with 5 stops of optical image stabilization, Nano USM motor, and dust/water-resistant construction. Includes lens hood and protective case.",
                    DailyRentalRate = 42.00m,
                    SecurityDeposit = 180.00m,
                    TotalQuantity = 2,
                    Condition = EquipmentCondition.Excellent,
                    Status = EquipmentStatus.Active,
                    IsActive = true,
                    Images = new List<EquipmentImage>
                    {
                        new() { ImageUrl = "https://images.unsplash.com/photo-1617005082133-548c4dd27f35?auto=format&fit=crop&w=800&q=80", IsPrimary = true, DisplayOrder = 1 }
                    }
                },
                new()
                {
                    BusinessId = biz1.Id,
                    CategoryId = catCameras.Id,
                    Title = "DJI RS 3 Pro 3-Axis Handheld Gimbal Stabilizer",
                    Description = "Supports payloads up to 4.5kg / 10lbs. Automated axis locks, carbon fiber construction, SuperSmooth stabilization, and focus motor compatibility.",
                    DailyRentalRate = 38.00m,
                    SecurityDeposit = 150.00m,
                    TotalQuantity = 3,
                    Condition = EquipmentCondition.Good,
                    Status = EquipmentStatus.Active,
                    IsActive = true,
                    Images = new List<EquipmentImage>
                    {
                        new() { ImageUrl = "https://images.unsplash.com/photo-1542751371-adc38448a05e?auto=format&fit=crop&w=800&q=80", IsPrimary = true, DisplayOrder = 1 }
                    }
                },

                // Store 2: Tools
                new()
                {
                    BusinessId = biz2.Id,
                    CategoryId = catTools.Id,
                    Title = "Bosch 1-5/8 in. SDS-Max Combination Rotary Hammer",
                    Description = "Vibration Control SDS-max combination rotary hammer drill. Excellent for concrete coring and demolition. Includes auxiliary handle and carrying case.",
                    DailyRentalRate = 52.00m,
                    SecurityDeposit = 175.00m,
                    TotalQuantity = 3,
                    Condition = EquipmentCondition.Good,
                    Status = EquipmentStatus.Active,
                    IsActive = true,
                    Images = new List<EquipmentImage>
                    {
                        new() { ImageUrl = "https://images.unsplash.com/photo-1504148455328-c376907d081c?auto=format&fit=crop&w=800&q=80", IsPrimary = true, DisplayOrder = 1 }
                    }
                },
                new()
                {
                    BusinessId = biz2.Id,
                    CategoryId = catTools.Id,
                    Title = "DeWalt 12-Inch Double-Bevel Sliding Compound Miter Saw",
                    Description = "Precise miter system with machined base fence support. Features 15 Amp 3800 RPM motor with cam-lock miter handle for quick and accurate angles.",
                    DailyRentalRate = 45.00m,
                    SecurityDeposit = 150.00m,
                    TotalQuantity = 2,
                    Condition = EquipmentCondition.LikeNew,
                    Status = EquipmentStatus.Active,
                    IsActive = true,
                    Images = new List<EquipmentImage>
                    {
                        new() { ImageUrl = "https://images.unsplash.com/photo-1581092160607-ee22621dd758?auto=format&fit=crop&w=800&q=80", IsPrimary = true, DisplayOrder = 1 }
                    }
                },
                new()
                {
                    BusinessId = biz2.Id,
                    CategoryId = catTools.Id,
                    Title = "Honda EU2200i Portable Super Quiet Inverter Generator",
                    Description = "2200 watts of super quiet, fuel-efficient, portable power. Operates at 48 to 57 dBA. Clean electricity for sensitive electronics and power tools.",
                    DailyRentalRate = 48.00m,
                    SecurityDeposit = 200.00m,
                    TotalQuantity = 4,
                    Condition = EquipmentCondition.Excellent,
                    Status = EquipmentStatus.Active,
                    IsActive = true,
                    Images = new List<EquipmentImage>
                    {
                        new() { ImageUrl = "https://images.unsplash.com/photo-1513694203232-719a280e022f?auto=format&fit=crop&w=800&q=80", IsPrimary = true, DisplayOrder = 1 }
                    }
                },

                // Store 3: Audio Visual
                new()
                {
                    BusinessId = biz3.Id,
                    CategoryId = catAv.Id,
                    Title = "Epson Pro EX9240 Full HD 1080p Wireless Projector",
                    Description = "4,000 lumens of color and white brightness. Full HD 1080p resolution, built-in wireless, HDMI and USB connectivity for presentations and movie nights.",
                    DailyRentalRate = 49.00m,
                    SecurityDeposit = 160.00m,
                    TotalQuantity = 3,
                    Condition = EquipmentCondition.LikeNew,
                    Status = EquipmentStatus.Active,
                    IsActive = true,
                    Images = new List<EquipmentImage>
                    {
                        new() { ImageUrl = "https://images.unsplash.com/photo-1517604931442-7e0c8ed2963c?auto=format&fit=crop&w=800&q=80", IsPrimary = true, DisplayOrder = 1 }
                    }
                },
                new()
                {
                    BusinessId = biz3.Id,
                    CategoryId = catAv.Id,
                    Title = "JBL EON715 15-inch 1300W Powered PA Speaker Pair",
                    Description = "1300W peak Class D amplification, built-in 3-channel digital mixer, Bluetooth 5.0 streaming, and speaker stands included. Ideal for events and concerts.",
                    DailyRentalRate = 79.00m,
                    SecurityDeposit = 250.00m,
                    TotalQuantity = 2,
                    Condition = EquipmentCondition.Good,
                    Status = EquipmentStatus.Active,
                    IsActive = true,
                    Images = new List<EquipmentImage>
                    {
                        new() { ImageUrl = "https://images.unsplash.com/photo-1545454675-3531b543be5d?auto=format&fit=crop&w=800&q=80", IsPrimary = true, DisplayOrder = 1 }
                    }
                },

                // Store 4 (PENDING BUSINESS) -> Should be hidden from public catalog!
                new()
                {
                    BusinessId = biz4.Id,
                    CategoryId = catTools.Id,
                    Title = "Hidden Pending Store Hammer Drill",
                    Description = "This item belongs to a pending business and must never appear in public discovery.",
                    DailyRentalRate = 20.00m,
                    SecurityDeposit = 50.00m,
                    TotalQuantity = 1,
                    Condition = EquipmentCondition.Fair,
                    Status = EquipmentStatus.Active,
                    IsActive = true
                },

                // Store 1 Inactive Listing -> Should be hidden from public catalog!
                new()
                {
                    BusinessId = biz1.Id,
                    CategoryId = catCameras.Id,
                    Title = "Draft Inactive Camera Rig",
                    Description = "This item is draft/inactive and must never appear in public discovery.",
                    DailyRentalRate = 99.00m,
                    SecurityDeposit = 100.00m,
                    TotalQuantity = 1,
                    Condition = EquipmentCondition.New,
                    Status = EquipmentStatus.Inactive,
                    IsActive = false
                }
            };

            var addedListings = false;
            foreach (var listing in listings)
            {
                if (!await context.Equipments.AnyAsync(e => e.Title == listing.Title))
                {
                    context.Equipments.Add(listing);
                    addedListings = true;
                }
            }

            if (addedListings)
            {
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded verified equipment catalog listings across approved and test businesses.");
            }
    }
}
