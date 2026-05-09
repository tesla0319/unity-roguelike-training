public static class GameConfig
{
    // Grid
    public const int GridSize = 12;

    // Floor
    public const int MaxFloor = 3;

    // Player
    public const int PlayerMaxHP = 100;
    public const int PlayerATK = 20;
    public const int PotionMaxStock = 2;
    public const int PotionHealAmount = 30;

    // Enemy
    public const int EnemyHP = 30;
    public const int EnemyATK = 10;
    public const int EnemyChaseRange = 6;
    public const float EnemyWanderRate = 0.3f;

    // Damage Floor
    public const int DamageFloorDamage = 5;

    // Combat
    public const float MissRate = 0.10f;
    public const float CritRate = 0.20f;
    public const float CritMultiplier = 1.5f;
    public const float DamageVarianceMin = 0.9f;
    public const float DamageVarianceMax = 1.1f;

    // Spawn
    public const int EnemyMinSpawnDistance = 4;

    // Map Generation
    public const int MapGenMaxRetries = 10;

    // Score
    public const int ScorePerKill = 10;
    public const int ScorePerFloor = 50;

    // Floor Configs (index 0 = 1F, 1 = 2F, 2 = 3F)
    public static readonly FloorConfig[] Floors = new[]
    {
        new FloorConfig { EnemyCount = 2, PotionCount = 4, DamageCount = 3, WallCount = 20 },
        new FloorConfig { EnemyCount = 3, PotionCount = 3, DamageCount = 4, WallCount = 24 },
        new FloorConfig { EnemyCount = 4, PotionCount = 2, DamageCount = 5, WallCount = 28 },
    };
}

[System.Serializable]
public class FloorConfig
{
    public int EnemyCount;
    public int PotionCount;
    public int DamageCount;
    public int WallCount;
}
