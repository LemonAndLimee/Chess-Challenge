using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

public class MyBot : IChessBot
{

    //Endgames and openings need work as they lead to repetition

    //endgame: incentivise moving opp. king to the edge plus moving our king closer to opp king

    //to do:
    //change tp table from dict to array
    //implement piece tables + endgame weighting
    //basic move ordering
    //iterative deepening + more move ordering

    const sbyte INVALID = 0, LBOUND = -1, UBOUND = 1, EXACT = 2;
    public struct Transposition
    {
        public Transposition(ulong zobristHash, int evaluation, byte d)
        {
            hash = zobristHash;
            score = evaluation;
            depth = (sbyte)d;
        }
        public ulong hash = 0;
        public int score = 0;
        public sbyte depth = 0;
        public sbyte flag = INVALID;
        public Move move = new Move();
    }
    ulong tpMask = 0x7FFFFF;
    Transposition[] transpositionTable = new Transposition[(ulong)0x7FFFFF + 1];

    PieceType[] types = new PieceType[] { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen, PieceType.King };
    short[] values = new short[] { 10, 30, 30, 50, 90, 200 };

    int calculations = 0;

    public Move Think(Board board, Timer timer)
    {
        calculations = 0;
        Search(board, 4, -10000, 10000);
        //Console.WriteLine("calc = " + calculations);
        return transpositionTable[board.ZobristKey & tpMask].move;
    }
    public int Search(Board b, int depth, int best, int upperBound) //lower bound is current best move, upper bound is best move of opponent
    {
        ref Transposition transposition = ref transpositionTable[b.ZobristKey & tpMask];
        if (transposition.flag != INVALID)
        {
            if (transposition.flag == EXACT) { return transposition.score; }
            else if (transposition.flag == LBOUND && transposition.score > best)
            {
                best = transposition.score;
            }
            else if (transposition.flag == UBOUND && transposition.score < upperBound)
            {
                upperBound = transposition.score;
            }

            if (best >= upperBound) { return transposition.score; }
        }
        if (b.IsInCheckmate())
        {
            return -1000;
        }

        if (depth == 0)
        {
            return EvaluateScore(b);
        }
        else
        {
            int bestScore = -10000;
            Move[] moves = b.GetLegalMoves();
            
            if (moves.Length == 0)
            {
                return 0;
            }
            Move bestMove = new Move();

            sbyte currentFlag = EXACT;
            foreach (Move move in moves)
            {
                calculations++;
                b.MakeMove(move);
                int score = -Search(b, depth - 1, -upperBound, -bestScore);
                b.UndoMove(move);
                if (score >= upperBound)
                {
                    bestScore = upperBound;
                    currentFlag = LBOUND;
                    break;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
            }
            if (depth % 2 == 0) //if depth is even number aka from bot's pov    later add storing positions for opponent too?
            {
                if (bestScore < best) { currentFlag = UBOUND; }
                transposition.score = bestScore;
                transposition.move = bestMove;
                transposition.hash = b.ZobristKey;
                transposition.flag = currentFlag;
            }
            return bestScore;
        }

    }

    public int EvaluateScore(Board board)
    {
        int score = 0;

        for (int i = 0; i < types.Length; i++)
        {
            //will be called from POV of person to play
            score += values[i] * board.GetPieceList(types[i], board.IsWhiteToMove).Count;
            score -= values[i] * board.GetPieceList(types[i], !board.IsWhiteToMove).Count;
        }
        return score;
    }

}