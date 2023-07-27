using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

public class MyBot : IChessBot
{

    //Endgames and openings need work as they lead to repetition

    //issue found with transposition table: move which is previously safe and now dangerous is considered safe by computer
    //thus allowing opponent to walk up and checkmate with no objections
    //only new moves are actually calculated
    //need a check on pieces newly in danger: bot will move a 0 score move even if a different piece is in danger
    //can be solved by storing positions not moves?

    //endgame: incentivise moving opp. king to the edge plus moving our king closer to opp king


    PieceType[] types = new PieceType[] { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen, PieceType.King };
    short[] values = new short[] { 10, 30, 30, 50, 90, 1000 };

    Dictionary<ulong, int> transpositionTable = new Dictionary<ulong, int>();
    Move currentBestMove;
    int calculations = 0;

    public Move Think(Board board, Timer timer)
    {
        calculations = 0;
        Search(board, 4, -10000, 10000);
        Console.WriteLine("dictionary = " + transpositionTable.Count);
        Console.WriteLine("calc = " + calculations);
        return currentBestMove;
    }
    public int Search(Board b, int depth, int best, int upperBound) //lower bound is current best move, upper bound is best move of opponent
    {
        if (transpositionTable.ContainsKey(b.ZobristKey))
        {
            return transpositionTable[b.ZobristKey];
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
            int bestScore = best;
            Move[] moves = b.GetLegalMoves();
            
            if (moves.Length == 0)
            {
                return 0;
            }
            Move bestMove = moves[0];

            foreach (Move move in moves)
            {
                calculations++;
                b.MakeMove(move);
                int score = -Search(b, depth - 1, -upperBound, -bestScore);
                if (score >= upperBound)
                {
                    bestScore = upperBound;
                    b.UndoMove(move);
                    break;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;
                }
                if (depth == 4)
                {
                    transpositionTable[b.ZobristKey] = bestScore;
                }
                b.UndoMove(move);
            }
            currentBestMove = bestMove;
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