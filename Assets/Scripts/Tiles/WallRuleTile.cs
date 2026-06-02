using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Wall Rule Tile", menuName = "2D/Tiles/Wall Rule Tile")]
public class WallRuleTile : RuleTile<WallRuleTile.Neighbor>
{
    [Header("Wall Group")]
    [Tooltip("Wall tiles with the same group are considered connected.")]
    [SerializeField] private string wallGroup = "Default";

    [Tooltip("If enabled, this tile connects to any WallRuleTile, regardless of group.")]
    [SerializeField] private bool connectToAnyWallRuleTile = false;

    [Tooltip("Optional extra tile assets that should also connect as This/NotThis.")]
    [SerializeField] private List<TileBase> additionalCompatibleTiles = new();

    public string WallGroup => wallGroup;

    public class Neighbor : RuleTile.TilingRule.Neighbor
    {
        public const int SameGroup = 3;
        public const int DifferentGroup = 4;
        public const int AnyWallRuleTile = 5;
    }

    public override bool RuleMatch(int neighbor, TileBase other)
    {
        TileBase resolved = ResolveTile(other);
        WallRuleTile otherWall = resolved as WallRuleTile;

        bool isAnyWall = otherWall != null;
        bool isSameGroup = IsSameGroup(otherWall);
        bool isCompatible = IsCompatibleThisMatch(resolved, otherWall, isSameGroup);

        switch (neighbor)
        {
            case TilingRuleOutput.Neighbor.This:
                return isCompatible;
            case TilingRuleOutput.Neighbor.NotThis:
                return !isCompatible;
            case Neighbor.SameGroup:
                return isSameGroup || IsAdditionalCompatibleTile(resolved);
            case Neighbor.DifferentGroup:
                return isAnyWall && !isSameGroup;
            case Neighbor.AnyWallRuleTile:
                return isAnyWall;
        }

        return base.RuleMatch(neighbor, resolved);
    }

    bool IsCompatibleThisMatch(TileBase resolved, WallRuleTile otherWall, bool isSameGroup)
    {
        if (resolved == this)
            return true;

        if (otherWall != null)
        {
            if (connectToAnyWallRuleTile || isSameGroup)
                return true;
        }

        if (additionalCompatibleTiles == null)
            return false;

        return IsAdditionalCompatibleTile(resolved);
    }

    bool IsAdditionalCompatibleTile(TileBase resolved)
    {
        if (additionalCompatibleTiles == null)
            return false;

        for (int i = 0; i < additionalCompatibleTiles.Count; i++)
        {
            if (additionalCompatibleTiles[i] == resolved)
                return true;
        }

        return false;
    }

    bool IsSameGroup(WallRuleTile otherWall)
    {
        if (otherWall == null)
            return false;

        return string.Equals(otherWall.wallGroup, wallGroup, System.StringComparison.Ordinal);
    }

    static TileBase ResolveTile(TileBase other)
    {
        if (other is RuleOverrideTile overrideTile)
        {
            if (overrideTile.m_Tile != null)
                return overrideTile.m_Tile;

            if (overrideTile.m_InstanceTile != null)
                return overrideTile.m_InstanceTile;
        }

        return other;
    }
}
