namespace MealsEnPlace.Api.Models.Entities;

/// <summary>
/// Physical storage location for an inventory item.
/// </summary>
public enum StorageLocation
{
    /// <summary>Below-freezing storage.</summary>
    Freezer,

    /// <summary>Refrigerated storage.</summary>
    Fridge,

    /// <summary>Ambient-temperature dry storage.</summary>
    Pantry
}
