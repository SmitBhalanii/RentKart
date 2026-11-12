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
            new() { Name = "Video & Studio Lighting", Slug = "video-lighting", Description = "Bi-color LED panels, softboxes, and C-stands for film and content creators.", IconClass = "video", DisplayOrder = 4, IsActive = true },
            new() { Name = "Lawn & Garden Machinery", Slug = "lawn-garden", Description = "Aerators, rototillers, leaf blowers, and pressure washers.", IconClass = "feather", DisplayOrder = 5, IsActive = true },
            new() { Name = "Generators & Power", Slug = "generators", Description = "Quiet portable inverter generators and temporary site power equipment.", IconClass = "zap", DisplayOrder = 6, IsActive = true },
            new() { Name = "Camping & Expedition", Slug = "camping-outdoor", Description = "Tents, portable power stations, and backpacking equipment.", IconClass = "compass", DisplayOrder = 7, IsActive = true },
            new() { Name = "Party & Staging", Slug = "party-staging", Description = "Tables, canopy tents, lighting rigs, and PA consoles.", IconClass = "gift", DisplayOrder = 8, IsActive = true },
            new() { Name = "Audio & PA Systems", Slug = "audio-equipment", Description = "Active powered PA speakers, wireless microphones, and mixers.", IconClass = "volume-2", DisplayOrder = 9, IsActive = true },
            new() { Name = "Construction Machinery", Slug = "construction-equipment", Description = "Concrete mixers, plate compactors, and masonry equipment.", IconClass = "truck", DisplayOrder = 10, IsActive = true }
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

        // 1. Purge transient test artifacts left by previous test executions
        await CleanupDevelopmentTestArtifactsAsync(context, logger);

        var catCameras = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "cameras-optics");
        var catTools = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "power-tools");
        var catAv = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "projectors-audio-visual");

        if (catCameras == null || catTools == null || catAv == null)
        {
            return;
        }

        var catVideo = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "video-lighting") ?? catCameras;
        var catConstruction = await context.Categories.FirstOrDefaultAsync(c => c.Slug == "construction-equipment") ?? catTools;

        // 2. Business 1 (Approved - Mumbai, Maharashtra)
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
                ShopName = "Bay City Camera & Cine Rentals - Mumbai",
                RegistrationNumber = "27AABCS1429B1Z1",
                ContactPhone = "+91 98201 12345",
                AddressLine = "Plot 42, Link Road",
                AddressLine2 = "Lokhandwala Complex, Andheri West",
                Landmark = "Near Oshiwara Metro",
                District = "Mumbai Suburban",
                City = "Mumbai",
                State = "Maharashtra",
                PostalCode = "400053",
                Country = "IN",
                OperatingHours = "Mon-Fri: 09:00 - 19:00, Sat: 09:30 - 17:00",
                PickupInstructions = "Suite 101, Near Oshiwara Metro. Government photo ID required at collection counter.",
                Status = BusinessStatus.Approved,
                ApprovedAtUtc = DateTime.UtcNow
            };
            context.BusinessProfiles.Add(biz1);
            await context.SaveChangesAsync();
        }
        else
        {
            // Update to Indian business details while keeping Bay City in ShopName for test compatibility
            biz1.ShopName = "Bay City Camera & Cine Rentals - Mumbai";
            biz1.AddressLine = "Plot 42, Link Road";
            biz1.AddressLine2 = "Lokhandwala Complex, Andheri West";
            biz1.Landmark = "Near Oshiwara Metro";
            biz1.District = "Mumbai Suburban";
            biz1.City = "Mumbai";
            biz1.State = "Maharashtra";
            biz1.PostalCode = "400053";
            biz1.Country = "IN";
            biz1.ContactPhone = "+91 98201 12345";
            biz1.Status = BusinessStatus.Approved;
            await context.SaveChangesAsync();
        }

        // 3. Business 2 (Approved - Bengaluru, Karnataka)
        // Keeps 'Apex Machinery' in ShopName to preserve existing test queries while anchoring to Bengaluru, India
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
                ShopName = "Apex Machinery & Equipment Rentals - Bengaluru",
                RegistrationNumber = "29AAACD4410E1Z5",
                ContactPhone = "+91 98450 67890",
                AddressLine = "128, 80 Feet Road",
                AddressLine2 = "Koramangala 4th Block",
                Landmark = "Near Sony World Junction",
                District = "Bengaluru Urban",
                City = "Bengaluru",
                State = "Karnataka",
                PostalCode = "560034",
                Country = "IN",
                OperatingHours = "Mon-Sat: 08:00 - 18:30",
                PickupInstructions = "Loading bay door 3 behind main showroom. Commercial transport assistance available.",
                Status = BusinessStatus.Approved,
                ApprovedAtUtc = DateTime.UtcNow
            };
            context.BusinessProfiles.Add(biz2);
            await context.SaveChangesAsync();
        }
        else
        {
            biz2.ShopName = "Apex Machinery & Equipment Rentals - Bengaluru";
            biz2.AddressLine = "128, 80 Feet Road";
            biz2.AddressLine2 = "Koramangala 4th Block";
            biz2.Landmark = "Near Sony World Junction";
            biz2.District = "Bengaluru Urban";
            biz2.City = "Bengaluru";
            biz2.State = "Karnataka";
            biz2.PostalCode = "560034";
            biz2.Country = "IN";
            biz2.ContactPhone = "+91 98450 67890";
            biz2.Status = BusinessStatus.Approved;
            await context.SaveChangesAsync();
        }

        // 4. Business 3 (Approved - New Delhi)
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
                AddressLine = "B-24, Outer Circle",
                AddressLine2 = "Block E, Connaught Place",
                Landmark = "Near Rajiv Chowk Metro Gate 5",
                District = "New Delhi",
                City = "New Delhi",
                State = "Delhi",
                PostalCode = "110001",
                Country = "IN",
                OperatingHours = "Mon-Fri: 09:30 - 19:30, Sun: 10:00 - 15:00",
                PickupInstructions = "Near Rajiv Chowk Metro Gate 5. Verification token required at reception.",
                Status = BusinessStatus.Approved,
                ApprovedAtUtc = DateTime.UtcNow
            };
            context.BusinessProfiles.Add(biz3);
            await context.SaveChangesAsync();
        }
        else
        {
            biz3.ShopName = "Delhi SoundWave & Stage AV Productions";
            biz3.AddressLine = "B-24, Outer Circle";
            biz3.AddressLine2 = "Block E, Connaught Place";
            biz3.Landmark = "Near Rajiv Chowk Metro Gate 5";
            biz3.District = "New Delhi";
            biz3.City = "New Delhi";
            biz3.State = "Delhi";
            biz3.PostalCode = "110001";
            biz3.Country = "IN";
            biz3.ContactPhone = "+91 98110 54321";
            biz3.Status = BusinessStatus.Approved;
            await context.SaveChangesAsync();
        }

        // 5. Business 4 (PendingApproval - Ahmedabad, Gujarat) -> For testing status filtering
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
                AddressLine = "405 Titanium City Centre",
                AddressLine2 = "SG Highway, Prahlad Nagar",
                Landmark = "Opp. Titanium Mall",
                District = "Ahmedabad",
                City = "Ahmedabad",
                State = "Gujarat",
                PostalCode = "380054",
                Country = "IN",
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

        // 6. Seed/Update Authentic Professional Equipment Catalog Listings with Local SVG Product Visuals
        var listings = new List<Equipment>
        {
            // Store 1: Cameras & Optics (Mumbai, Maharashtra)
            new()
            {
                BusinessId = biz1.Id,
                CategoryId = catCameras.Id,
                Title = "Sony Alpha a7 IV Full-Frame Mirrorless Camera",
                Description = "33MP Exmor R CMOS sensor, 4K 60p 10-bit recording, Real-time Eye AF for photo and video. Includes 2 high-capacity batteries, dual USB-C charger, and 128GB V90 UHS-II SD card.",
                DailyRentalRate = 2500.00m,
                SecurityDeposit = 10000.00m,
                TotalQuantity = 3,
                Condition = EquipmentCondition.LikeNew,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "/images/catalog/sony-a7iv.jpg", IsPrimary = true, DisplayOrder = 1 }
                }
            },
            new()
            {
                BusinessId = biz1.Id,
                CategoryId = catCameras.Id,
                Title = "Canon RF 24-70mm f/2.8L IS USM Zoom Lens",
                Description = "Standard professional zoom lens with 5 stops of optical image stabilization, Nano USM autofocus motor, and dust/moisture-resistant construction. Includes petal lens hood and protective soft case.",
                DailyRentalRate = 1800.00m,
                SecurityDeposit = 7500.00m,
                TotalQuantity = 2,
                Condition = EquipmentCondition.Excellent,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "/images/catalog/canon-rf-24-70.jpg", IsPrimary = true, DisplayOrder = 1 }
                }
            },
            new()
            {
                BusinessId = biz1.Id,
                CategoryId = catCameras.Id,
                Title = "DJI RS 3 Pro 3-Axis Handheld Gimbal Stabilizer",
                Description = "Supports cinema payloads up to 4.5kg / 10lbs. Features automated axis locks, layered carbon fiber arms, SuperSmooth stabilization mode, and focus motor compatibility.",
                DailyRentalRate = 1500.00m,
                SecurityDeposit = 6000.00m,
                TotalQuantity = 3,
                Condition = EquipmentCondition.Good,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "/images/catalog/dji-rs3-pro.jpg", IsPrimary = true, DisplayOrder = 1 }
                }
            },

            // Store 2: Tools, Machinery & Generators (Bengaluru, Karnataka)
            new()
            {
                BusinessId = biz2.Id,
                CategoryId = catTools.Id,
                Title = "Bosch Professional Rotary Hammer Drill",
                Description = "1-5/8 inch SDS-Max combination rotary hammer drill with Vibration Control. Designed for heavy concrete coring, demolition, and anchor setting. Includes auxiliary D-handle and hard carry case.",
                DailyRentalRate = 1200.00m,
                SecurityDeposit = 5000.00m,
                TotalQuantity = 3,
                Condition = EquipmentCondition.Good,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "/images/catalog/bosch-rotary-hammer.jpg", IsPrimary = true, DisplayOrder = 1 }
                }
            },
            new()
            {
                BusinessId = biz2.Id,
                CategoryId = catTools.Id,
                Title = "DeWalt 12-Inch Double-Bevel Sliding Compound Miter Saw",
                Description = "Precise miter saw system with tall sliding fence support and 15 Amp 3800 RPM motor. Double-bevel design with stainless steel miter detent plate for high-precision woodwork and trim.",
                DailyRentalRate = 1400.00m,
                SecurityDeposit = 5500.00m,
                TotalQuantity = 2,
                Condition = EquipmentCondition.LikeNew,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "/images/catalog/dewalt-miter-saw.jpg", IsPrimary = true, DisplayOrder = 1 }
                }
            },
            new()
            {
                BusinessId = biz2.Id,
                CategoryId = catTools.Id,
                Title = "Honda EU2200i Portable Super Quiet Inverter Generator",
                Description = "2200 watts peak / 1800 watts rated portable clean power. Ultra-quiet 48 dB operation with Eco-Throttle technology. Perfect for outdoor events, film productions, and sensitive electronics.",
                DailyRentalRate = 2200.00m,
                SecurityDeposit = 8000.00m,
                TotalQuantity = 4,
                Condition = EquipmentCondition.Excellent,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "/images/catalog/honda-eu2200i.jpg", IsPrimary = true, DisplayOrder = 1 }
                }
            },
            new()
            {
                BusinessId = biz2.Id,
                CategoryId = catConstruction.Id,
                Title = "Heavy-Duty Portable Concrete Mixer (140L)",
                Description = "Rugged 140-liter drum mixer with 0.75 HP electric motor. Equipped with heavy-duty solid rubber wheels and tipping handwheel for commercial site mixing, masonry, and plastering.",
                DailyRentalRate = 1600.00m,
                SecurityDeposit = 6500.00m,
                TotalQuantity = 2,
                Condition = EquipmentCondition.Good,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "/images/catalog/concrete-mixer.jpg", IsPrimary = true, DisplayOrder = 1 }
                }
            },

            // Store 3: Audio Visual & Lighting (New Delhi)
            new()
            {
                BusinessId = biz3.Id,
                CategoryId = catAv.Id,
                Title = "Epson Pro EX9240 Full HD 1080p Wireless Projector",
                Description = "4,000 lumens of color and white brightness with 3LCD 3-chip technology. Full HD 1080p resolution, built-in Miracast wireless streaming, dual HDMI, and 16W internal speaker.",
                DailyRentalRate = 1800.00m,
                SecurityDeposit = 6000.00m,
                TotalQuantity = 3,
                Condition = EquipmentCondition.LikeNew,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "/images/catalog/epson-projector.jpg", IsPrimary = true, DisplayOrder = 1 }
                }
            },
            new()
            {
                BusinessId = biz3.Id,
                CategoryId = catAv.Id,
                Title = "JBL EON715 15-inch 1300W Powered PA Loudspeaker Pair",
                Description = "Pair of 1300W peak Class D powered speakers with built-in 3-channel digital mixer, dbx DriveRack DSP, Bluetooth 5.0 streaming, and heavy-duty adjustable speaker stands.",
                DailyRentalRate = 3200.00m,
                SecurityDeposit = 12000.00m,
                TotalQuantity = 2,
                Condition = EquipmentCondition.Good,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "/images/catalog/jbl-eon715.jpg", IsPrimary = true, DisplayOrder = 1 }
                }
            },
            new()
            {
                BusinessId = biz3.Id,
                CategoryId = catVideo.Id,
                Title = "Professional Bi-Color LED Video Lighting Kit (3-Point)",
                Description = "3-light bi-color continuous LED panel kit with adjustable 3200K - 5600K color temperature, CRI 97+, barn doors, diffusion softboxes, 2-meter air-cushioned stands, and padded transport case.",
                DailyRentalRate = 2000.00m,
                SecurityDeposit = 7000.00m,
                TotalQuantity = 3,
                Condition = EquipmentCondition.LikeNew,
                Status = EquipmentStatus.Active,
                IsActive = true,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "/images/catalog/video-lighting-kit.jpg", IsPrimary = true, DisplayOrder = 1 }
                }
            },

            // Store 1 Inactive Listing -> Hidden from public discovery, used to test draft/inactive security
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
                IsActive = false,
                Images = new List<EquipmentImage>
                {
                    new() { ImageUrl = "/images/categories/cameras-optics.svg", IsPrimary = true, DisplayOrder = 1 }
                }
            }
        };

        foreach (var listing in listings)
        {
            var existing = await context.Equipments
                .Include(e => e.Images)
                .FirstOrDefaultAsync(e => e.Title == listing.Title);

            if (existing == null)
            {
                context.Equipments.Add(listing);
            }
            else
            {
                // Update existing record with authoritative clean data and local images
                existing.BusinessId = listing.BusinessId;
                existing.CategoryId = listing.CategoryId;
                existing.Description = listing.Description;
                existing.DailyRentalRate = listing.DailyRentalRate;
                existing.SecurityDeposit = listing.SecurityDeposit;
                existing.Condition = listing.Condition;
                existing.Status = listing.Status;
                existing.IsActive = listing.IsActive;

                // Always ensure authoritative local real product image is applied
                context.EquipmentImages.RemoveRange(existing.Images);
                existing.Images = listing.Images;
            }
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded and verified authentic equipment catalog listings across approved Indian businesses.");
    }

    private static async Task CleanupDevelopmentTestArtifactsAsync(ApplicationDbContext context, ILogger logger)
    {
        try
        {
            var testEquipments = await context.Equipments
                .Include(e => e.Images)
                .Include(e => e.MaintenanceRecords)
                .Include(e => e.BookingItems)
                    .ThenInclude(bi => bi.BookingGroup)
                .Where(e => (e.Title.StartsWith("Dedicated Test Item") ||
                             e.Title.StartsWith("Image Management Test") ||
                             e.Title.StartsWith("Maintenance Test Gear") ||
                             e.Title == "Updated Title Safely" ||
                             e.Title.Contains(" - Test ") ||
                             e.Title.StartsWith("Milwaukee M18") ||
                             e.Title.StartsWith("Hidden Pending Store") ||
                             e.Title.Contains("e75a40d85a63413ebcd1bfc54") ||
                             e.Title.Contains("dfc6f16533144212ab719b2644c") ||
                             e.Title.Contains("182af3ddc7d846cb87011a92f9") ||
                             e.Title.Contains("682b1d8521d0451f8676ca0d7c") ||
                             e.Title.Contains("71b4300e3f1d47b6ab108168fd"))
                            && e.Title != "Draft Inactive Camera Rig")
                .ToListAsync();

            if (testEquipments.Any())
            {
                logger.LogInformation("Purging {Count} residual development/test equipment listings from catalog database...", testEquipments.Count);
                foreach (var eq in testEquipments)
                {
                    context.MaintenanceRecords.RemoveRange(eq.MaintenanceRecords);
                    context.EquipmentImages.RemoveRange(eq.Images);

                    if (eq.BookingItems.Any())
                    {
                        foreach (var bi in eq.BookingItems)
                        {
                            var reviews = await context.Reviews.Where(r => r.BookingItemId == bi.Id).ToListAsync();
                            context.Reviews.RemoveRange(reviews);

                            var docs = await context.RentalDocuments.Where(d => d.BookingGroupId == bi.BookingGroupId).ToListAsync();
                            context.RentalDocuments.RemoveRange(docs);

                            context.BookingItems.Remove(bi);
                        }
                    }

                    context.Equipments.Remove(eq);
                }
                await context.SaveChangesAsync();
                logger.LogInformation("Development test listings purged successfully.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Note: Clean-up of residual development test listings encountered non-critical exception. Skipping.");
        }
    }
}
