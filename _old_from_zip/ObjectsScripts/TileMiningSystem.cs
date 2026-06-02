using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum TileMineStatus
{
    None,
    MissingTilemap,
    NoTileAtCell,
    NoRuleForTile,
    WrongTool,
    NotEnoughPower,
    Mined
}

[System.Serializable]
public class TileMiningRule
{
    [Tooltip("Tile asset to mine (can be RuleTile).")]
    public TileBase tile;

    [Tooltip("Required tool type. None means any tool.")]
    public ToolType requiredTool = ToolType.None;

    [Min(1)]
    [Tooltip("Minimum tool power required to mine this tile.")]
    public int minToolPower = 1;

    [Tooltip("Drops returned when tile is mined.")]
    public Drop[] drops;
}

public struct TileMineResult
{
    public TileMineStatus status;
    public Vector3Int cell;
    public Vector3 worldPosition;
    public Drop[] drops;

    public bool IsMined => status == TileMineStatus.Mined;
}

public class TileMiningSystem : MonoBehaviour
{
    [Header("Tilemap")]
    [Tooltip("Tilemap handled by this mining system. If empty, first Tilemap on this object or children is used.")]
    [SerializeField] private Tilemap targetTilemap;

    [Header("Rules")]
    [Tooltip("Mapping: tile -> required tool/power + drop table.")]
    [SerializeField] private List<TileMiningRule> rules = new();

    void Awake()
    {
        if (targetTilemap == null)
            targetTilemap = GetComponentInChildren<Tilemap>(true);
    }

    public bool TryMineAtWorld(Vector3 worldPosition, ItemData toolItem, out TileMineResult result)
    {
        if (targetTilemap == null)
        {
            result = new TileMineResult
            {
                status = TileMineStatus.MissingTilemap
            };
            return false;
        }

        Vector3Int cell = targetTilemap.WorldToCell(worldPosition);
        return TryMine(cell, toolItem, out result);
    }

    public bool TryMine(Vector3Int cell, ItemData toolItem, out TileMineResult result)
    {
        result = new TileMineResult
        {
            status = TileMineStatus.None,
            cell = cell
        };

        if (targetTilemap == null)
        {
            result.status = TileMineStatus.MissingTilemap;
            return false;
        }

        TileBase currentTile = WorldGrid.GetTile(cell, targetTilemap);
        if (currentTile == null)
        {
            result.status = TileMineStatus.NoTileAtCell;
            return false;
        }

        if (!TryGetRule(currentTile, out TileMiningRule rule))
        {
            result.status = TileMineStatus.NoRuleForTile;
            return false;
        }

        if (!MeetsToolRequirement(toolItem, rule, out TileMineStatus failStatus))
        {
            result.status = failStatus;
            return false;
        }

        if (!WorldGrid.RemoveTile(cell, targetTilemap))
        {
            result.status = TileMineStatus.NoTileAtCell;
            return false;
        }

        result.status = TileMineStatus.Mined;
        result.worldPosition = targetTilemap.GetCellCenterWorld(cell);
        result.drops = rule.drops;
        return true;
    }

    bool TryGetRule(TileBase tile, out TileMiningRule rule)
    {
        rule = null;
        if (tile == null || rules == null)
            return false;

        for (int i = 0; i < rules.Count; i++)
        {
            TileMiningRule candidate = rules[i];
            if (candidate == null || candidate.tile == null)
                continue;

            if (candidate.tile == tile)
            {
                rule = candidate;
                return true;
            }
        }

        return false;
    }

    bool MeetsToolRequirement(ItemData toolItem, TileMiningRule rule, out TileMineStatus failStatus)
    {
        failStatus = TileMineStatus.None;

        if (toolItem == null || toolItem.type != ItemType.Tool)
        {
            failStatus = TileMineStatus.WrongTool;
            return false;
        }

        if (rule.requiredTool != ToolType.None && toolItem.toolType != rule.requiredTool)
        {
            failStatus = TileMineStatus.WrongTool;
            return false;
        }

        int minPower = Mathf.Max(1, rule.minToolPower);
        if (toolItem.toolPower < minPower)
        {
            failStatus = TileMineStatus.NotEnoughPower;
            return false;
        }

        return true;
    }
}
