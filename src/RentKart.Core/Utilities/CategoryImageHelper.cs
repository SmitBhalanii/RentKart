namespace RentKart.Core.Utilities;

public static class CategoryImageHelper
{
    public static string GetCategoryFallbackImage(string? categorySlug)
    {
        if (string.IsNullOrWhiteSpace(categorySlug))
        {
            return "/images/categories/default.svg";
        }

        var normalized = categorySlug.Trim().ToLowerInvariant();

        if (normalized.Contains("camera") || normalized.Contains("optic") || normalized.Contains("photo"))
            return "/images/categories/cameras-optics.svg";

        if (normalized.Contains("tool") || normalized.Contains("drill") || normalized.Contains("saw"))
            return "/images/categories/power-tools.svg";

        if (normalized.Contains("projector") || normalized.Contains("audio-visual") || normalized.Contains("av"))
            return "/images/categories/projectors-audio-visual.svg";

        if (normalized.Contains("audio") || normalized.Contains("sound") || normalized.Contains("speaker"))
            return "/images/categories/audio-equipment.svg";

        if (normalized.Contains("light") || normalized.Contains("video"))
            return "/images/categories/video-lighting.svg";

        if (normalized.Contains("generator") || normalized.Contains("power"))
            return "/images/categories/generators.svg";

        if (normalized.Contains("construction") || normalized.Contains("concrete") || normalized.Contains("mixer"))
            return "/images/categories/construction-equipment.svg";

        if (normalized.Contains("lawn") || normalized.Contains("garden"))
            return "/images/categories/lawn-garden.svg";

        if (normalized.Contains("computer") || normalized.Contains("laptop") || normalized.Contains("workstation"))
            return "/images/categories/computers-laptops.svg";

        if (normalized.Contains("party") || normalized.Contains("staging") || normalized.Contains("event"))
            return "/images/categories/party-staging.svg";

        if (normalized.Contains("industrial") || normalized.Contains("machinery"))
            return "/images/categories/industrial-machinery.svg";

        if (normalized.Contains("camp") || normalized.Contains("outdoor"))
            return "/images/categories/camping-outdoor.svg";

        return "/images/categories/default.svg";
    }

    public static bool IsCategoryVisual(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return true;

        var normalized = imageUrl.Trim().ToLowerInvariant();
        return normalized.Contains("/images/categories/") || 
               normalized.Contains("/images/placeholder") || 
               normalized.EndsWith("default.svg");
    }
}

