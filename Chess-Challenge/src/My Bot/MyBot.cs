using ChessChallenge.API;
using System;
using System.Collections.Generic;

public class MyBot : IChessBot
{

    //Issue: doesn't seem to recognise dangerous moves, ignores pieces in danger (due to lack of assessment of position it leaves it on)
    //Endgames and openings need work as they lead to repetition

    //had one case of illegal move when trying to ladder checkmate black with two queens vs 1 king
    //currently does worse with a depth of 4 than a depth of 2

    PieceType[] types = new PieceType[] { PieceType.None, PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen, PieceType.King };
    short[] values = new short[] { 0, 10, 30, 30, 50, 90, 100 };

    Dictionary<Move, int> transpositionTable = new Dictionary<Move, int>();

    public Move Think(Board board, Timer timer)
    {
        return CalculateMove(board, 4);
    }

    public Move CalculateMove(Board b, int depth)
    {
        int d = depth - 1;
        Move[] moves = b.GetLegalMoves();

        Move bestMove = new Move();
        int highestScore = -1000;

        foreach (Move move in moves)
        {
            int worstCase = 1000;
            if (transpositionTable.ContainsKey(move))
            {
                //use score from table
                worstCase = transpositionTable[move];
            }
            else
            {
                //work out, then add to table
                b.MakeMove(move);
                if (b.IsInCheckmate())
                {
                    b.UndoMove(move);
                    return move;
                }
                int score1 = GetScore(move);
                foreach (Move opponentMove in b.GetLegalMoves())
                {
                    b.MakeMove(opponentMove);
                    if (b.IsInCheckmate())
                    {
                        worstCase = -10000;
                    }
                    else
                    {
                        int score2 = score1 - GetScore(opponentMove);
                        if (d > 0)
                        {
                            score2 += GetScore(CalculateMove(b, d));
                        }

                        if (score2 < worstCase)
                        {
                            worstCase = score2;
                        }
                        if (score2 < highestScore)
                        {
                            b.UndoMove(opponentMove);
                            break;
                        }
                    }
                    b.UndoMove(opponentMove);
                }
                //what about old moves that now mean something different? they will be in the table but contain wrong values
                //unsure if move data type contains the piece info or not
                transpositionTable[move] = worstCase;
                b.UndoMove(move);
            }
            

            if (worstCase >= highestScore)
            {
                highestScore = worstCase;
                bestMove = move;
            }
        }

        Console.WriteLine("dict length = " + transpositionTable.Count);

        return bestMove;
    }
    public int GetScore(Move m)
    {
        int score = 0;
        if (m.IsCapture)
        {
            score += values[Array.IndexOf(types, m.CapturePieceType)];
        }
        if (m.IsPromotion)
        {
            score += values[Array.IndexOf(types, m.PromotionPieceType)];
        }
        return score;
    }

    /**accounted for piece captures, piece promotions, checkmates
     * not yet accounted for checks, might be needed?
     * also no code specifically for endgames
     * 
     * 
     * function f(b, depth): depth = 1 means check my moves and opps responses
     *  depth--
     *  get legal moves
     *  
     *  highest score;
     *  hs move;
     *  foreach legal move:
     *      make move
     *      if board is in checkmate then undo move, return move
     *      s1 = score(move)
     *      worst case; 
     *      foreach now legal move (opp turn)
     *          make opp move
     *          if board in checkmate then worst case = -10000
     *          else then
     *              s2 = s1 - score(oppmove)  //s2 is new current score from our pov
     *              if depth > 0 then
     *                  s2 += score(f(b, depth))
     *              if s2 <= worst case then worst case = s2
     *          undo opp move
     *      
     *      if worst case > highest score then highest score = worst case, hs move = move
     *      undo move
     *  
     *  return hs move */

    /** scoring function(b, m):
     *  score = 0
     *  if capture then score += capture piece worth
     *  if promotion then score += promotion piece worth
     *  
     *  return score
     */



}