using System;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Tag-tree decoder for JPEG 2000 (JPX) packet headers.
/// Tag-trees are used to efficiently encode inclusion and zero bit-plane information.
/// </summary>
internal sealed class JpxTagTree
{
    private readonly int _width;
    private readonly int _height;
    private readonly JpxTagTreeNode[] _nodes;
    private readonly int _totalNodes;

    /// <summary>
    /// Represents a node in the tag tree structure.
    /// </summary>
    private struct JpxTagTreeNode
    {
        public int Value;
        public int LowerBound;
        public bool Known;
        public int Parent;
    }

    /// <summary>
    /// Creates a new tag tree for the specified dimensions.
    /// </summary>
    /// <param name="width">Width of the leaf level (e.g., number of precincts horizontally).</param>
    /// <param name="height">Height of the leaf level (e.g., number of precincts vertically).</param>
    public JpxTagTree(int width, int height)
    {
        _width = width;
        _height = height;

        // Calculate total number of nodes needed for the complete tree
        int totalNodes = 0;
        int levelWidth = width;
        int levelHeight = height;

        // Build tree level by level from leaves to root
        while (levelWidth > 0 && levelHeight > 0)
        {
            totalNodes += levelWidth * levelHeight;
            
            // Stop when we reach root (1x1)
            if (levelWidth == 1 && levelHeight == 1)
            {
                break;
            }
            
            levelWidth = (levelWidth + 1) / 2;
            levelHeight = (levelHeight + 1) / 2;
        }

        _totalNodes = totalNodes;
        _nodes = new JpxTagTreeNode[totalNodes];
        InitializeTree();
    }

    /// <summary>
    /// Initializes the tree structure with parent-child relationships.
    /// </summary>
    private void InitializeTree()
    {
        int levelStart = 0;
        int levelWidth = _width;
        int levelHeight = _height;

        // Build tree bottom-up (leaves first)
        while (levelWidth > 0 && levelHeight > 0)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                for (int x = 0; x < levelWidth; x++)
                {
                    int currentNode = levelStart + y * levelWidth + x;
                    _nodes[currentNode].Value = int.MaxValue;
                    _nodes[currentNode].LowerBound = 0;
                    _nodes[currentNode].Known = false;

                    // Calculate parent node index (in the next level up)
                    if (levelWidth > 1 || levelHeight > 1)
                    {
                        int nextLevelWidth = (levelWidth + 1) / 2;
                        int nextLevelHeight = (levelHeight + 1) / 2;
                        int nextLevelStart = levelStart + levelWidth * levelHeight;
                        
                        int parentX = x / 2;
                        int parentY = y / 2;
                        _nodes[currentNode].Parent = nextLevelStart + parentY * nextLevelWidth + parentX;
                    }
                    else
                    {
                        // Root level (1x1)
                        _nodes[currentNode].Parent = -1;
                    }
                }
            }

            // Stop when we reach root (1x1)
            if (levelWidth == 1 && levelHeight == 1)
            {
                break;
            }

            levelStart += levelWidth * levelHeight;
            levelWidth = (levelWidth + 1) / 2;
            levelHeight = (levelHeight + 1) / 2;
        }
    }

    /// <summary>
    /// Decodes a value from the tag tree for the specified leaf position.
    /// Uses the standard root-to-leaf algorithm per ITU-T T.800 Annex B.10.2.
    /// </summary>
    /// <param name="bitReader">Bit reader positioned at the tag tree data.</param>
    /// <param name="leafX">X coordinate of the leaf node.</param>
    /// <param name="leafY">Y coordinate of the leaf node.</param>
    /// <param name="threshold">Threshold value to decode up to.</param>
    /// <returns>True if the leaf value is less than or equal to the threshold.</returns>
    public bool DecodeValue(ref JpxBitReader bitReader, int leafX, int leafY, int threshold)
    {
        if (leafX < 0 || leafX >= _width || leafY < 0 || leafY >= _height)
        {
            return false;
        }

        int leafIndex = leafY * _width + leafX;

        // Build path from leaf to root
        Span<int> path = stackalloc int[32]; // Max tree depth
        int pathLength = 0;
        int current = leafIndex;

        while (current >= 0 && current < _totalNodes)
        {
            path[pathLength++] = current;
            current = _nodes[current].Parent;
        }

        // Decode from root to leaf (reverse order)
        for (int i = pathLength - 1; i >= 0; i--)
        {
            int nodeIndex = path[i];
            ref var node = ref _nodes[nodeIndex];

            if (node.Known)
            {
                continue;
            }

            // Propagate lower bound from parent
            if (node.Parent >= 0 && node.Parent < _totalNodes)
            {
                ref var parentNode = ref _nodes[node.Parent];
                if (parentNode.LowerBound > node.LowerBound)
                {
                    node.LowerBound = parentNode.LowerBound;
                }
            }

            // Decode bits until value is known or lower bound reaches threshold
            // Per ITU-T T.800 Annex B.10.2:
            //   0 bit → value > current state (not yet equal, increment lower bound)
            //   1 bit → value = current state (value is known)
            while (node.LowerBound < threshold && !node.Known)
            {
                int bit = bitReader.ReadBit();
                if (bit == 1)
                {
                    // Value equals current lower bound
                    node.Value = node.LowerBound;
                    node.Known = true;
                }
                else
                {
                    // Value is greater, increment lower bound
                    node.LowerBound++;
                }
            }
        }

        ref var leaf = ref _nodes[leafIndex];
        return leaf.Known && leaf.Value <= threshold;
    }

    /// <summary>
    /// Decodes and returns the actual value for the specified leaf position.
    /// Keeps decoding until the leaf value is known.
    /// </summary>
    /// <param name="bitReader">Bit reader positioned at the tag tree data.</param>
    /// <param name="leafX">X coordinate of the leaf node.</param>
    /// <param name="leafY">Y coordinate of the leaf node.</param>
    /// <returns>The decoded value for the leaf.</returns>
    public int DecodeAbsoluteValue(ref JpxBitReader bitReader, int leafX, int leafY)
    {
        if (leafX < 0 || leafX >= _width || leafY < 0 || leafY >= _height)
        {
            return 0;
        }

        int leafIndex = leafY * _width + leafX;
        ref var leaf = ref _nodes[leafIndex];

        if (leaf.Known)
        {
            return leaf.Value;
        }

        // Decode with increasing thresholds until leaf is known
        // Use a reasonable upper bound (bit depths rarely exceed 32)
        for (int threshold = leaf.LowerBound; threshold < 256; threshold++)
        {
            DecodeValue(ref bitReader, leafX, leafY, threshold);

            if (leaf.Known)
            {
                return leaf.Value;
            }
        }

        return leaf.LowerBound;
    }

    /// <summary>
    /// Resets the tree for a new decoding session.
    /// </summary>
    public void Reset()
    {
        for (int i = 0; i < _totalNodes; i++)
        {
            _nodes[i].Value = int.MaxValue;
            _nodes[i].LowerBound = 0;
            _nodes[i].Known = false;
        }
    }
}