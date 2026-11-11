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
    public static async Task SeedCategoriesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CatalogDataSeeder");

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
    }

    public static async Task SeedCatalogDataAsync(IServiceProvider serviceProvider)
    {
        await SeedCategoriesAsync(serviceProvider);

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CatalogDataSeeder");

        var catCameras = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "cameras-optics");
        var catTools = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "power-tools");
        var catAv = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "projectors-audio-visual");

        if (catCameras == null || catTools == null || catAv == null)
        {
            return;
        }

        // Create Business 1 (Approved - Mumbai, Maharashtra)
        var store1Email = "store.sf@rentkart.local";
        var user1 = await userManager.FindByEmailAsync(store1Email);
        if (user1 == null)
        {
            user1 = new ApplicationUser
            {
                UserName = store1Email,
                Email = store1Email,
                FullName = "Marcus Vance",
                PhoneNumber = "+91 98201 12345",
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
                ShopName = "Mumbai Cine Gear & Pro Camera Rentals",
                RegistrationNumber = "27AABCS1429B1Z1",
                ContactPhone = "+91 98201 12345",
                AddressLine = "Plot 42, Link Road, Andheri West",
                City = "Mumbai",
                State = "Maharashtra",
                PostalCode = "400053",
                OperatingHours = "Mon-Fri: 09:00 - 19:00, Sat: 09:30 - 17:00",
                PickupInstructions = "Suite 101, Near Oshiwara Metro. Government photo ID required at collection counter.",
                Status = BusinessStatus.Approved,
                ApprovedAtUtc = DateTime.UtcNow
            };
            context.BusinessProfiles.Add(biz1);
            await context.SaveChangesAsync();
        }

        // Create Business 2 (Approved - Bengaluru, Karnataka)
        var store2Email = "store.sj@rentkart.local";
        var user2 = await userManager.FindByEmailAsync(store2Email);
        if (user2 == null)
        {
            user2 = new ApplicationUser
            {
                UserName = store2Email,
                Email = store2Email,
                FullName = "David Chen",
                PhoneNumber = "+91 98450 67890",
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
                ShopName = "Bengaluru Power Tools & Machinery Hub",
                RegistrationNumber = "29AAACD4410E1Z5",
                ContactPhone = "+91 98450 67890",
                AddressLine = "128, 80 Feet Road, Koramangala 4th Block",
                City = "Bengaluru",
                State = "Karnataka",
                PostalCode = "560034",
                OperatingHours = "Mon-Sat: 08:00 - 18:30",
                PickupInstructions = "Loading bay door 3 behind main showroom. Commercial transport assistance available.",
                Status = BusinessStatus.Approved,
                ApprovedAtUtc = DateTime.UtcNow
            };
            context.BusinessProfiles.Add(biz2);
            await context.SaveChangesAsync();
        }

        // Create Business 3 (Approved - New Delhi)
        var store3Email = "store.oak@rentkart.local";
        var user3 = await userManager.FindByEmailAsync(store3Email);
        if (user3 == null)
        {
            user3 = new ApplicationUser
            {
                UserName = store3Email,
                Email = store3Email,
                FullName = "Elena Rostova",
                PhoneNumber = "+91 98110 54321",
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
                ShopName = "Delhi SoundWave & Stage AV Productions",
                RegistrationNumber = "07AAACR9102L1Z9",
                ContactPhone = "+91 98110 54321",
                AddressLine = "B-24, Outer Circle, Connaught Place",
                City = "New Delhi",
                State = "Delhi",
                PostalCode = "110001",
                OperatingHours = "Mon-Fri: 09:30 - 19:30, Sun: 10:00 - 15:00",
                PickupInstructions = "Near Rajiv Chowk Metro Gate 5. Verification token required at reception.",
                Status = BusinessStatus.Approved,
                ApprovedAtUtc = DateTime.UtcNow
            };
            context.BusinessProfiles.Add(biz3);
            await context.SaveChangesAsync();
        }

        // Create Business 4 (PendingApproval - Ahmedabad, Gujarat) -> For testing status filtering
        var store4Email = "store.pending@rentkart.local";
        var user4 = await userManager.FindByEmailAsync(store4Email);
        if (user4 == null)
        {
            user4 = new ApplicationUser
            {
                UserName = store4Email,
                Email = store4Email,
                FullName = "Pending Owner",
                PhoneNumber = "+91 98980 11223",
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
                ShopName = "Gujarat Industrial Equipment Rental",
                ContactPhone = "+91 98980 11223",
                AddressLine = "405 Titanium City Centre, SG Highway",
                City = "Ahmedabad",
                State = "Gujarat",
                PostalCode = "380054",
                OperatingHours = "Mon-Fri: 09:00 - 18:00",
                Status = BusinessStatus.PendingApproval
            };
            context.BusinessProfiles.Add(biz4);
            await context.SaveChangesAsync();
        }
        else if (biz4.Status != BusinessStatus.PendingApproval)
        {
            biz4.Status = BusinessStatus.PendingApproval;
            biz4.ApprovedAtUtc = null;
            await context.SaveChangesAsync();
        }

        // Seed Active Equipment for Approved Businesses with ₹ INR pricing
        var listings = new List<Equipment>
        {
            // Store 1: Cameras (Mumbai)
            new()
            {
                BusinessId = biz1.Id,
                CategoryId = catCameras.Id,
                Title = "Sony Alpha a7 IV Full-Frame Hybrid Camera",
                Description = "33MP Exmor R CMOS sensor, 4K 60p 10-bit recording, Real-time Eye AF for photo and video. Includes 2 batteries, dual charger, and 128GB V90 SD card.",
                DailyRentalRate = 2500.00m,
                SecurityDeposit = 10000.00m,
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
                DailyRentalRate = 1800.00m,
                SecurityDeposit = 7500.00m,
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
                DailyRentalRate = 1500.00m,
                SecurityDeposit = 6000.00m,
                TotalQuantity = 3,
                Condition = EquipmentCondition.Good,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "https://images.unsplash.com/photo-1542751371-adc38448a05e?auto=format&fit=crop&w=800&q=80", IsPrimary = true, DisplayOrder = 1 }
                }
            },

            // Store 2: Tools (Bengaluru)
            new()
            {
                BusinessId = biz2.Id,
                CategoryId = catTools.Id,
                Title = "Bosch 1-5/8 in. SDS-Max Combination Rotary Hammer",
                Description = "Vibration Control SDS-max combination rotary hammer drill. Excellent for concrete coring and demolition. Includes auxiliary handle and carrying case.",
                DailyRentalRate = 1200.00m,
                SecurityDeposit = 5000.00m,
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
                Description = "Precise miter system with machined base fence support. Features 15 Amp motor with cam-lock miter handle for quick and accurate angles.",
                DailyRentalRate = 1400.00m,
                SecurityDeposit = 5500.00m,
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
                Description = "2200 watts of super quiet, fuel-efficient, portable power. Clean electricity for sensitive electronics, events, and construction sites.",
                DailyRentalRate = 2200.00m,
                SecurityDeposit = 8000.00m,
                TotalQuantity = 4,
                Condition = EquipmentCondition.Excellent,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "https://images.unsplash.com/photo-1513694203232-719a280e022f?auto=format&fit=crop&w=800&q=80", IsPrimary = true, DisplayOrder = 1 }
                }
            },

            // Store 3: Audio Visual (New Delhi)
            new()
            {
                BusinessId = biz3.Id,
                CategoryId = catAv.Id,
                Title = "Epson Pro EX9240 Full HD 1080p Wireless Projector",
                Description = "4,000 lumens of color and white brightness. Full HD 1080p resolution, built-in wireless, HDMI and USB connectivity for presentations and movie nights.",
                DailyRentalRate = 1800.00m,
                SecurityDeposit = 6000.00m,
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
                DailyRentalRate = 3200.00m,
                SecurityDeposit = 12000.00m,
                TotalQuantity = 2,
                Condition = EquipmentCondition.Good,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "https://images.unsplash.com/photo-1545454675-3531b543be5d?auto=format&fit=crop&w=800&q=80", IsPrimary = true, DisplayOrder = 1 }
                }
            },

            // Store 4 (PENDING BUSINESS) -> Must be hidden from public catalog
            new()
            {
                BusinessId = biz4.Id,
                CategoryId = catTools.Id,
                Title = "Hidden Pending Store Hammer Drill",
                Description = "This item belongs to a pending business and must never appear in public discovery.",
                DailyRentalRate = 800.00m,
                SecurityDeposit = 2000.00m,
                TotalQuantity = 1,
                Condition = EquipmentCondition.Fair,
                Status = EquipmentStatus.Active,
                IsActive = true
            },

            // Store 1 Inactive Listing -> Must be hidden from public catalog
            new()
            {
                BusinessId = biz1.Id,
                CategoryId = catCameras.Id,
                Title = "Draft Inactive Camera Rig",
                Description = "This item is draft/inactive and must never appear in public discovery.",
                DailyRentalRate = 3500.00m,
                SecurityDeposit = 5000.00m,
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
            logger.LogInformation("Seeded verified equipment catalog listings across approved Indian businesses.");
        }
    }
}
