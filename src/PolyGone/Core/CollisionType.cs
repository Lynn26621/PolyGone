namespace PolyGone;

public enum CollisionType
{
    None = 0,
    Solid = 1,      // Normal solid collision
    SemiSolid = 2,  // Drop-through platforms
    Slippery = 3,   // Slippery surface (e.g., ice)
    Bouncy = 4,     // Bouncy surface 
    Damage = 5,     // Damaging surface
}

public static class CollisionTypeMapper
{
    // Map tileset tile IDs to collision types (after subtracting firstgid)
    // CollisionTiles.tsx defines: tile 0=Solid, tile 1=SemiSolid
    public static CollisionType GetCollisionType(int tileId)
    {
        return tileId switch
        {
            0 => CollisionType.Solid,
            1 => CollisionType.SemiSolid,
            2 => CollisionType.Slippery,
            3 => CollisionType.Bouncy,
            4 => CollisionType.Damage,
            _ => CollisionType.None,
        };
    }
}
