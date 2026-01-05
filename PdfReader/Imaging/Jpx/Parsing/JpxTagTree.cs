using System;

namespace PdfReader.Imaging.Jpx.Parsing;

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
    /// </summary>
    /// <param name="bitReader">Bit reader positioned at the tag tree data.</param>
    /// <param name="leafX">X coordinate of the leaf node.</param>
    /// <param name="leafY">Y coordinate of the leaf node.</param>
    /// <param name="threshold">Threshold value to decode against.</param>
    /// <returns>True if the decoded value is less than the threshold.</returns>
    public bool DecodeValue(ref JpxBitReader bitReader, int leafX, int leafY, int threshold)
    {
        if (leafX < 0 || leafX >= _width || leafY < 0 || leafY >= _height)
        {
            return false;
        }

        int leafIndex = leafY * _width + leafX;
        return DecodeNodeValue(ref bitReader, leafIndex, threshold);
    }

    /// <summary>
    /// Decodes the value for a specific node in the tree.
    /// </summary>
    private bool DecodeNodeValue(ref JpxBitReader bitReader, int nodeIndex, int threshold)
    {
        if (nodeIndex >= _totalNodes)
        {
            return false;
        }

        ref var node = ref _nodes[nodeIndex];

        // If we already know the value is below threshold
        if (node.Known && node.Value < threshold)
        {
            return true;
        }

        // If lower bound is already at or above threshold
        if (node.LowerBound >= threshold)
        {
            return false;
        }

        // Need to decode more bits
        while (node.LowerBound < threshold && !node.Known)
        {
            // Check parent first (if exists) to maintain tree invariants
            if (node.Parent >= 0)
            {
                if (!DecodeNodeValue(ref bitReader, node.Parent, node.LowerBound + 1))
                {
                    // Parent's value is <= our lower bound, so our value is also <= lower bound
                    node.Value = node.LowerBound;
                    node.Known = true;
                    break;
                }
            }

            // Read a bit to determine if value is greater than current lower bound
            int bit = bitReader.ReadBit();
            if (bit == 0)
            {
                // Value equals lower bound
                node.Value = node.LowerBound;
                node.Known = true;
            }
            else
            {
                // Value is greater, increment lower bound
                node.LowerBound++;
            }
        }

        return node.Known && node.Value < threshold;
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