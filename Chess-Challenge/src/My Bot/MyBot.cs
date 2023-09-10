using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class MyBot : IChessBot
{

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

    // Point values for each piece
    readonly short[] values = { 100, 320, 330, 500, 900, 20000 };

    // Piece-Square-Tables using the Simplified Evaluation Function online
    ulong[] pieceTables = new ulong[]{
        
        0,
        3617008641903833650,
        723412809732590090,
        361706447983740165,
        86234890240,
        431208669220633349,
        363114732645386757,
        0,
        
        14904912430879660238,
        15630868406696209624,
        16285027312364814562,
        16286440206365558242,
        16285032831482003682,
        16286434687248369122,
        15630868428254932184,
        14904912430879660238,

        17075106577787582188,
17726168133330272502,
17726173674006184182,
17727581048889738742,
17726179171564650742,
17728993921331759862,
17727575508213827062,
17075106577787582188,

        0,
363113758191127045,
18086456103519912187,
18086456103519912187,
18086456103519912187,
18086456103519912187,
18086456103519912187,
21558722560,

        17075106599346304748,
17726168133330272502,
17726173652447461622,
18086461622637101307,
18086461622637101056,
17726173652447462902,
17726168133330600182,
17075106599346304748,
        
        16346053230286395618,
16346053230286395618,
16346053230286395618,
16346053230286395618,
17069454958667162348,
17792856730165374198,
1446781380292776980,
1449607125176819220
    };

    Move rootMove;
    
    float maxSearchTime = 0;
    Timer globalTimer;

    bool printFlag = true;

    List<string> lines = new List<string>();

    public Move Think(Board board, Timer timer)
    {
        lines.Clear();
        lines.Add("\n ---------------\n current board eval= " + EvaluateScore(board) + "\n");

        /**byte[] decompressed = pieceTables.SelectMany(BitConverter.GetBytes).ToArray();
        sbyte[] pieceTable = (sbyte[])(Array)decompressed;

        Console.WriteLine("king:");
        for (int x = 0; x < 8; x++)
        {
            string outp = "";
            for (int y = 0; y < 8; y++)
            {
                outp = outp + pieceTable[(x * 8) + y + 320] + ", ";
            }
            Console.WriteLine(outp);
        }
        Piece king = board.GetPieceList(PieceType.King, false)[0];

        Console.WriteLine("\nNow on " + king.Square.Name);
        Console.WriteLine(GetPieceTableIndex(king, 5));
        Console.WriteLine(pieceTable[GetPieceTableIndex(king, 5)]);*/

        maxSearchTime = timer.MillisecondsRemaining / 30;
        globalTimer = timer;
        
        for (int depth = 2, alpha = -99999, beta = 99999; ;)
        {
            lines.Add(depth.ToString());
            //Console.WriteLine(depth + " search with alpha beta = " + alpha + ", " + beta);

            int score = Search(board, depth, 0, alpha, beta);

            if (timer.MillisecondsElapsedThisTurn >= maxSearchTime)
            {
#if DEBUG
                board.MakeMove(rootMove);
                lines.Add(String.Format("\n RETURN {0}, eval score after move= {1}", rootMove, EvaluateScore(board)));
                board.UndoMove(rootMove);

                string docpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                using (StreamWriter outputFile = new StreamWriter(Path.Combine(docpath, "ChessProgramOutput.txt"), true))
                {
                    foreach (string line in lines)
                    {
                        outputFile.WriteLine(line);
                    }
                }
#endif
                return rootMove;
            }

#if DEBUG
            lines.Add(String.Format("Info: Depth={0} || Score={1} || Rootmove={2}{3} || alphabeta={4} {5}", depth, score, rootMove.StartSquare.Name, rootMove.TargetSquare.Name, alpha, beta));
            lines.Add(String.Format("Move List: {0}", GetMoveList(board, depth)));
            lines.Add("");
            //Console.WriteLine("Info: Depth={0} || Score={1} || Rootmove={2}{3} || alphabeta={4} {5}", depth, score, rootMove.StartSquare.Name, rootMove.TargetSquare.Name, alpha, beta);
            //Console.WriteLine("Move List: {0}", GetMoveList(board, depth));
#endif

            if (score <= alpha) alpha -= 100;
            else if (score >= beta) beta += 100;
            else
            {
                alpha = score - 25;
                beta = score + 25;
                depth += 2;
            }

        }
    }

#if DEBUG
    string GetMoveList(Board board, int depth)
    {
        string result = "";
        Move m = transpositionTable[board.ZobristKey & tpMask].move;
        int score = transpositionTable[board.ZobristKey & tpMask].score;
        result = result + m.StartSquare.Name + m.TargetSquare.Name + " ";

        board.MakeMove(m);
        result = result + EvaluateScore(board) + " " + score;
        if (depth > 1)
        {
            result = result + ", " + GetMoveList(board, depth - 1);
        }
        else
        {
            result = result + ", quiesce = " + Quiesce(board, -9999, 9999);
        }
        board.UndoMove(m);

        return result;
    }
#endif

    // Negamax search with alpha-beta pruning
    int Search(Board b, int depth, int ply, int alpha, int beta)
    {
        bool isRoot = ply++ == 0;

        // If the position occurred previously its score is stored in the transposition table
        ref Transposition transposition = ref transpositionTable[b.ZobristKey & tpMask];
        
        if (transposition.flag != INVALID && transposition.depth >= depth)
        {
            // If the stored position score is exact, return said score
            // If the score is a lower bound higher than beta, use that
            // If the score is an upper bound lower than alpha, use that
            if (transposition.flag == EXACT || (transposition.flag == LBOUND && transposition.score >= beta) || (transposition.flag == UBOUND && transposition.score <= alpha))
            {
                if (isRoot)
                {
                    lines.Add("transposition inherit root " + transposition.move + " depth = " + transposition.depth + " score = " + transposition.score + " flag = " + transposition.flag);
                    //Console.WriteLine("transposition inherit root " + transposition.move + " depth = " + transposition.depth + " score = " + transposition.score + " flag = " + transposition.flag);
                    rootMove = transposition.move;
                }
                return transposition.score;
            }

        }
        // If there is no valid transposition table entry stored:

        // Check for checkmate and draws
        if (b.IsInCheckmate())
        {
            return -99999;
        }
        if (b.IsDraw())
        {
            return 0;
        }
        // If the end of the search is reached, perform a Quiescence Search
        if (depth == 0)
        {
            return -Quiesce(b, -beta, -alpha); //UNSURE WHAT SYMBOLS TO USE HERE (PROBLEM WITH KING WANDERING + QUEEN MAKING SACRIFICS)
        }
        else
        {
            int startingAlpha = alpha;
            int bestScore = -10000;
            Move[] moves = OrderMoves(b, transposition);
            
            Move bestMove = moves[0];

            foreach (Move move in moves)
            {
                // If out of time, return checkmate (positive value as it's from the opponent's perspective)
                // Check that depth > 2 to confirm the bot has found a move
                if (depth > 2 && globalTimer.MillisecondsElapsedThisTurn >= maxSearchTime)
                {
                    lines.Add("TIME CUTOFF");
                    return 99999;
                }

                if (depth ==4 && isRoot)
                {
                    //printFlag = true;
                }

                b.MakeMove(move);

                if (printFlag && depth > 1)
                {
                    string output = "";
                    for (int i = 0; i < ply; i++)
                    {
                        output = output + "  ";
                    }
                    lines.Add(String.Format(output + "GOING INTO {0} at depth, ply {1} {2}, ab= {3} {4} ....................................... FEN {5}", move, depth, ply - 1, beta, alpha, b.GetFenString()));
                }

                int score = -Search(b, depth - 1, ply, -beta, -alpha);
#if DEBUG       
                if (move.StartSquare.Name == "f4" && move.TargetSquare.Name == "c7" && depth == 1 && ply == 4)
                {
                    //Console.WriteLine("CASE STUDY F4C7: {0}, evalatcall= {1}, quiesce= {2}, nextSearch= {3}", score, EvaluateScore(b), Quiesce(b, -beta, -alpha), -Search(b, depth-1, ply, -beta, -alpha));
                }
                if (printFlag)
                {
                    string output = "";
                    for (int i = 0; i < ply; i++)
                    {
                        output = output + "  ";
                    }
                    if (depth > 1)
                    {
                        lines.Add(String.Format(output + "{0} at depth, ply {1} {2}, with score {3}", move, depth, ply - 1, score));
                    }
                    else if (depth == 1)
                    {
                        lines.Add(String.Format(output + "{0} at depth, ply {1} {2}, with score {3} --- quiesce params= {6} {4} {5}", move, depth, ply - 1, score, alpha, beta, EvaluateScore(b)));
                    }
                    //Console.WriteLine(output + "{0} at depth, ply {1} {2}, with score {3}", move, depth, ply - 1, score);
                    if (isRoot)
                    {
                        //printFlag = false;
                    }
                }
#endif
                //b.UndoMove(move);


                // If the score exceeds beta, we know it is "too good" and therefore cut off the rest of the search
                if (score > beta)
                {
                    bestScore = beta;
                    bestMove = move;
                    b.UndoMove(move);
                    break;
                }
                // Update best move/score and prune if alpha >= beta
                if (score > bestScore)
                {
                    if (printFlag)
                    {
                        lines.Add(String.Format("new best move found at depth {7} : {0}{1} with score= {2}, prev bestscore/move= {3}, {4}{5}, alphabeta= {6} {8}",
                                move.StartSquare.Name, move.TargetSquare.Name, score, bestScore,
                                bestMove.StartSquare.Name, bestMove.TargetSquare.Name, alpha, depth, beta));
                        if (depth == 1)
                        {
                            Quiesce(b, alpha, beta, true);
                        }
                        //Console.WriteLine("new best move found at depth {7} : {0}{1} with score= {2}, prev bestscore/move= {3}, {4}{5}, alphabeta= {6} {8}",
                                //move.StartSquare.Name, move.TargetSquare.Name, score, bestScore,
                                //bestMove.StartSquare.Name, bestMove.TargetSquare.Name, alpha, depth, beta);

                    }
                    bestScore = score;
                    bestMove = move;

                    // Improve alpha
                    alpha = Math.Max(alpha, score);
                    if (alpha >= beta)
                    {
                        b.UndoMove(move);
                        break;
                    }
                }

                b.UndoMove(move);
            }

            // Add entry to transposition table
            if (bestScore < startingAlpha) transposition.flag = UBOUND;
            else if (bestScore > beta) transposition.flag = LBOUND;
            else transposition.flag = EXACT;
            transposition.score = bestScore;
            transposition.move = bestMove;
            transposition.hash = b.ZobristKey;
            transposition.depth = (sbyte)depth;

            if (isRoot) rootMove = bestMove;

            return bestScore;
        }

    }

    // Orders moves based on a MVV-LVA algorithm
    Move[] OrderMoves(Board board, Transposition currentTransposition)
    {
        Move[] legalMoves = board.GetLegalMoves();
        int[] moveScores = new int[legalMoves.Length];

        for (int i = 0; i < legalMoves.Length; i++)
        {
            Move move = legalMoves[i];
            // Prioritise the previously searched best move (using a very small number because the array is sorted in ascending order)
            if (currentTransposition.move == move) moveScores[i] = -1000;
            else if (move.IsCapture) moveScores[i] = values[(int)move.MovePieceType - 1] - 5 * values[(int)move.CapturePieceType - 1];
            else moveScores[i] = 200;
        }

        Array.Sort(moveScores, legalMoves);
        return legalMoves;
    }

    // Quiescence Search to avoid the Horizon Effect, where there is a blunder just past the search depth
    int Quiesce(Board b, int alpha, int beta)
    {
        int stand_pat = EvaluateScore(b);
        if (stand_pat >= beta) return beta;
        if (stand_pat > alpha) alpha = stand_pat;

        foreach (Move move in b.GetLegalMoves(true))
        {
            b.MakeMove(move);
            int score = -Quiesce(b, -beta, -alpha);
            b.UndoMove(move);

            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }
        return alpha;
    }
#if DEBUG
    int Quiesce(Board b, int alpha, int beta, bool debug)
    {
        lines.Add("**************** QUIESCE bscore, ab = " + EvaluateScore(b) + ",  " + alpha + ", " + beta);
        int stand_pat = EvaluateScore(b);
        if (stand_pat >= beta) return beta;
        if (stand_pat > alpha) alpha = stand_pat;

        foreach (Move move in b.GetLegalMoves(true))
        {
            b.MakeMove(move);
            lines.Add("              QUIESCE " + move + " :::");
            int score = -Quiesce(b, -beta, -alpha, true);
            lines.Add("              " + move + " with quiesce score of " + score);
            b.UndoMove(move);

            if (score >= beta)
            {
                lines.Add("              score >= beta, return beta= " + beta);
                return beta;
            }
            if (score > alpha)
            {
                lines.Add("              score > alpha, alpha= score");
                alpha = score;
            }
        }
        lines.Add("**************** END QUIESCE, return alpha= " + alpha);
        return alpha;
    }
#endif

    int EvaluateScore(Board board)
    {
        int score = 0;

        PieceList[] pieceLists = board.GetAllPieceLists();
        int multiplier = board.IsWhiteToMove ? -1 : 1;

        byte[] decompressed = pieceTables.SelectMany(BitConverter.GetBytes).ToArray();
        sbyte[] pieceTable = (sbyte[])(Array)decompressed;


        for (int i = 0; i < pieceLists.Length; i++)
        {
            if (i == 6) multiplier = multiplier * -1; //when colour switches 
            

            foreach (Piece piece in pieceLists[i])
            {
                //RETURNING PST VALUES THAT ARENT EVEN IN ANY TABLE??
                if (piece.IsKing && piece.IsWhite && (piece.Square.Name == "e1" || piece.Square.Name == "f1"))
                {
                    //Console.WriteLine("white king on {0}, pst of {1}", piece.Square.Name, pieceTable[(i % 6 * 8) + GetPieceTableIndex(piece, i < 6)]);
                }
                float increment = pieceTable[GetPieceTableIndex(piece, i%6)] + values[i % 6];
                //float increment = values[i % 6];
                score += (int)(multiplier * increment);
            }
        }
        return score;
    }

    int GetPieceTableIndex(Piece p, int pieceNumber)
    {
        int index = !p.IsWhite ? (p.Square.Rank * 8) + 7 - p.Square.File : ((7 - p.Square.Rank) * 8) + p.Square.File;
        return index + pieceNumber*64;
    }


}