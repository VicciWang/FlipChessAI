using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UIElements;

public class MonteAI : Player
{
    public int expansion;

    private static double Cp = 1 / Mathf.Sqrt(2);
    private double[,] lookupTable = new double[1500, 1500];

    private int numLookups = 0;
    private int numNoLookups = 0;
    private void Awake()
    {
        for (int i = 1; i < 1500; ++i)
            for (int j = i; j < 1500; ++j)
                lookupTable[i, j] = (Cp * Math.Sqrt((Math.Log((double)i)) / (double)j));
    }
    //apply equation
    public double getRHS(int n, int nj)
    {
        if (n < 1500)
        {
            numLookups++;
            return lookupTable[n, nj];
        }
        numNoLookups++;
        return (2 * Cp * Math.Sqrt((2 * (Math.Log((double)n))) / (double)nj));
    }
    //generate AI move
    public Vector2 AImove()
    {
        Vector2 move = new Vector2();

        MonteCarloNode rootNode = new MonteCarloNode();

        //simulate xxx times
        for (int i = 0; i < expansion; i++)
        {
            MonteCarloNode next = TreePolicy(rootNode);
            int a = Simulate(next);
            next.BackUp(a);
        }
        
        MonteCarloNode max = null;
        double maxValue = double.NegativeInfinity;

        //calculate each nodes weight, return the biggest
        foreach(MonteCarloNode node in rootNode.children)
        {
            if (node.visitTimes == 0)
                continue;
            if ((double)node.score / (double)node.visitTimes > maxValue)
            {
                max = new MonteCarloNode(node);
                maxValue = (double)node.score / (double)node.visitTimes;
            }
            /*
            double utc = ((double)node.score / (double)node.visitTimes) + getRHS(rootNode.visitTimes, node.visitTimes);

            if (utc > maxValue)
            {
                max = node;
                maxValue = utc;
            }*/
        }

        move = max.pos;

        return move;
    }
    //if the current node is not leaf node, expand the tree
    public MonteCarloNode TreePolicy(MonteCarloNode root)
    {
        MonteCarloNode v = root;
        Board.Instance.CheckAllowedMoves();

        while (Board.Instance.availableMoves.Count != 0)
        {
            Board.Instance.CheckAllowedMoves();
            if(Board.Instance.availableMoves.Count != 0)
                return v.Expand(Board.Instance.availableMoves);
        }
        return v.BestChild();
    }
    //monte carlo simulating, go to final state, adding rewards regards to different ending
    public int Simulate(MonteCarloNode node)
    {
        Board.isSimulating = true;
        Board.Instance.CheckAllowedMoves();

        while (Board.Instance.availableMoves.Count != 0)
        {
            //randomly choose a node to play
            List<Vector2> potentialMoves = Board.Instance.availableMoves;
            int i = UnityEngine.Random.Range(0, potentialMoves.Count - 1);

            Board.Instance.StartPlay(new int[2] { (int)potentialMoves[i].x, (int)potentialMoves[i].y });
            Board.Instance.CheckAllowedMoves();
        }

        
        //reset board every time finishing simulation
        Board.Instance.ResetBoard();
        if (Board.Instance.WhiteCount == Board.Instance.BlackCount)
            return 0;
        return Board.Instance.WhiteCount > Board.Instance.BlackCount ? -1 : 1;
    }
    //override playchess function in player.cs
    public override void PlayChess()
    {
        Vector2 nextMove = new Vector2();
        nextMove = AImove();

        //check allowed moves before step chess 
        Board.Instance.CheckAllowedMoves();
        Board.isSimulating = false;

        Board.Instance.StartPlay(new int[2] { (int)nextMove.x, (int)nextMove.y });
    }
}
public class MonteCarloNode
{
    public int visitTimes;//visit++
    public int score;//add up when back up

    public MonteCarloNode parent;
    public List<MonteCarloNode> children;
    public Vector2 pos;

    public MonteAI ai;

    //init new node
    public MonteCarloNode()
    {
        visitTimes = 0;
        score = 0;
        parent = null;
        children = new List<MonteCarloNode>();
        pos = new Vector2();

        Board.Instance.CheckAllowedMoves();
        for (int i = 0; i < Board.Instance.availableMoves.Count; i++)
        {
            MonteCarloNode temp = new MonteCarloNode(this, Board.Instance.availableMoves[i]);
            children.Add(temp);
        }
    }
    //init new node has parent
    public MonteCarloNode(MonteCarloNode Parent, Vector2 vec) 
    {
        visitTimes = 0;
        score = 0;
        parent = Parent;
        children = new List<MonteCarloNode>();
        this.pos = vec;
        Board.Instance.CheckAllowedMoves();
    }
    //copy a node
    public MonteCarloNode(MonteCarloNode node)
    {
        visitTimes = node.visitTimes;
        score = node.score;
        parent = node.parent;
        children = node.children;
        pos = node.pos;
    }
    //for node back up, recursively adding reward to parent node
    public void BackUp(int value)
    {
        score += value;
        visitTimes++;

        if (parent != null)
        {
            parent.BackUp(value);
        }
    }
    //expand the current node, randomly adding available move to child node.
    public MonteCarloNode Expand(List<Vector2> nextMoves)
    {
        if (nextMoves.Count > 0)
        {
            int r = UnityEngine.Random.Range(0, nextMoves.Count - 1);
            MonteCarloNode temp = new MonteCarloNode(this, nextMoves[r]);
            nextMoves.Remove(nextMoves[r]);
            children.Add(temp);

            return temp;
        }
        return null;
    }
    //Upper Confidence bound applied to Trees
    public MonteCarloNode BestChild()
    {
        double bestVal = double.MinValue;
        MonteCarloNode bestChild = null;

        Debug.Log("Best");
        foreach (MonteCarloNode node in children)
        {
            double uct = ((double)node.score / (double)node.visitTimes) + ai.getRHS(visitTimes, node.visitTimes);

            if (uct > bestVal)
            {
                bestChild = node;
                bestVal = uct;
            }
        }
        return bestChild;
    }
}
