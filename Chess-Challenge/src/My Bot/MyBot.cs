using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{

    //Endgames and openings need work as they lead to repetition

    //endgame: incentivise moving opp. king to the edge plus moving our king closer to opp king

    //to do:
    //change tp table from dict to array : DONE
    //implement piece tables + endgame weighting
    //basic move ordering
    //iterative deepening + more move ordering

    //still a decision making issue - the bot forked my queen then ignored the capture, moving a pawn instead

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

    PieceType[] types = { PieceType.Pawn, PieceType.Knight, PieceType.Bishop, PieceType.Rook, PieceType.Queen, PieceType.King };
    byte[] values = { 10, 30, 30, 50, 90, 200 };

    int calculations = 0;

    ulong[][] pieceTables = {
        new ulong[]{
        0,
        3617008641903833650,
        723412809732590090,
        361706447983740165,
        86234890240,
        431208669220633349,
        363114732645386757,
        0
        },
        new ulong[] {
        14904912430879660238,
        15630868406696209624,
        16285027312364814562,
        16286440206365558242,
        16285032831482003682,
        16286434687248369122,
        15630868428254932184,
        14904912430879660238
    },
        new ulong[] {17075106577787582188,
17726168133330272502,
17726173674006184182,
17727581048889738742,
17726179171564650742,
17728993921331759862,
17727575508213827062,
17075106577787582188},
        new ulong[]{0,
363113758191127045,
18086456103519912187,
18086456103519912187,
18086456103519912187,
18086456103519912187,
18086456103519912187,
21558722560},
        new ulong[] {17075106599346304748,
17726168133330272502,
17726173652447461622,
18086461622637101307,
18086461622637101056,
17726173652447462902,
17726168133330600182,
17075106599346304748},
        new ulong[] {
        16346053230286395618,
16346053230286395618,
16346053230286395618,
16346053230286395618,
17069454958667162348,
17792856730165374198,
1446781380292776980,
1449607125176819220
    }
    };
    
    public Move Think(Board board, Timer timer)
    {
        //byte[] decom = pieceTableBishop.SelectMany(BitConverter.GetBytes).ToArray();
        //sbyte[] result = (sbyte[])(Array)decom;
        //Array.ForEach(result, x => Console.Write("" + x + ","));

        calculations = 0;
        Search(board, 4, -10000, 10000);
        //Console.WriteLine("calc = " + calculations);
        Console.WriteLine(transpositionTable[board.ZobristKey & tpMask].score);
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
                    bestMove = move;
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

        PieceList[] pieceLists = board.GetAllPieceLists();
        float multiplier = board.IsWhiteToMove ? 0.1f : -0.1f;
        for (int i = 0; i < pieceLists.Length; i++)
        {
            if (i == 6) //when colour switches
            {
                multiplier = multiplier * -1;
            }
            byte[] decompressed = pieceTables[i%6].SelectMany(BitConverter.GetBytes).ToArray();
            sbyte[] currentPieceTable = (sbyte[])(Array)decompressed;
            foreach (Piece piece in pieceLists[i])
            {
                score += (int)(multiplier * currentPieceTable[GetPieceTableIndex(piece, board.IsWhiteToMove)]) + values[i%6];
            }
        }
        return score;
    }

    public int GetPieceTableIndex(Piece p, bool isWhite)
    {
        int index = isWhite ? (p.Square.Rank * 8) + 7 - p.Square.File : ((7 - p.Square.Rank) * 8) + p.Square.File;
        return index;
    }


}