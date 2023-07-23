using ChessChallenge.API;
using System;

public class MyBot : IChessBot
{
    Move bestmoveRoot = Move.NullMove;
    ulong[] hashStack = new ulong[1000];
    int[] pieceVal = {0, 100, 310, 330, 500, 1000, 10000 };
    ulong[] psts = {657614902731556116, 420894376522008539, 384592972454655708, 294230846293738582, 2652419541464331472, 2671849832919671889, 366006826859316500, 786039115310605588, 421220596516513823, 347715422320145685, 2653835368843789523, 2653835507373790419, 18446551955868304660, 2746413614358180687, 657087419442874070, 402634039558219357, 2634972355714442514, 347398415096309077, 2598925273659680147, 2435392984242242641, 2616931218801624272, 402598361179960661, 402611904285332822, 311114878130394196, 2544596548924023886, 2578681335195702417, 2798786050398176144, 349144996867285910, 2689578365922927891, 384307098409076181, 402035762391246293, 310547319660279064, 2653540560124322898, 347988028138534036, 2690150177382155539, 329700955074409748, 2617234825795687634, 2652982006052898003, 2691861228011467922, 456959123538409047, 2634985896656598419, 347411539376043476, 365993490884412629, 2509443101639631827, 2690127773809853585, 384307167128540502, 2653835437563532563, 2671568361113076947, 365716485979591956, 2671845367142233299, 2635535165996999826, 457508666683229268, 2617270292511273937, 329397220424906068, 2653545024726316435, 366288232009245908, 2617525097938900175, 2726464632624588114, 422042614432880023, 384602117564863767, 419223676167484820, 366270574426682966, 311391607134967189, 2562901352686613519, 2654684255053505931, 2635834091375506641, 2598648609230906641, 2544314865591047122, 2671832102050796690, 2546047977320407827, 383444869581206549, 384601907111994452, 401758895947998613, 420608368187635159, 402607437519607191, 311682226146837652};

    struct TTEntry {
        ulong key;
        public Move move;

        public TTEntry(ulong _key, Move _move) {key = _key; move = _move; }
    }

    const int entries = (1 << 18);
    ulong nodes = 0;
    TTEntry[] tt = new TTEntry[entries];

    public int getPstVal(int psq) {
        return (int)(psts[psq / 10] >> (6 * (psq % 10))) & 63;
    }

    public int Evaluate(Board board) {
        int eval = 0;
        for(var p = PieceType.Pawn; p <= PieceType.King; p++) {
            eval += pieceVal[(int)p] * (board.GetPieceList(p, true).Count - board.GetPieceList(p, false).Count);
            ulong mask = board.GetPieceBitboard(p, true);
            while(mask != 0)
                eval += getPstVal(128 * ((int)p - 1) + BitboardHelper.ClearAndGetIndexOfLSB(ref mask)) * 8 - 160;
            mask = board.GetPieceBitboard(p, false);
            while(mask != 0)
                eval -= getPstVal(128 * ((int)p - 1) + 56 ^ BitboardHelper.ClearAndGetIndexOfLSB(ref mask)) * 8 - 160;
        }

        if(!board.IsWhiteToMove)
            eval *= -1;
        return eval;
    }
    public int Search(Board board, Timer timer, int alpha, int beta, int depth, int ply) {
        nodes++;
        ulong key = board.ZobristKey;
        hashStack[board.PlyCount] = key;
        bool qsearch = (depth <= 0);
        int best = -30000;

        if(ply > 0) {
            for(int i = board.PlyCount - 2; i >= 0; i -= 2) {
                if(hashStack[i] == hashStack[board.PlyCount])
                    return 0;
            }
        }

        if(qsearch) {
            best = Evaluate(board);
            if(best >= beta) return best;
            if(best > alpha) alpha = best;
        }

        TTEntry entry = tt[key % entries];
        Move[] moves = board.GetLegalMoves(qsearch);
        int[] scores = new int[moves.Length];
        for(int i = 0; i < moves.Length; i++) {
            if(moves[i] == entry.move) scores[i] = 1000000;
            else scores[i] = 100 * moves[i].TargetSquare.Index - moves[i].StartSquare.Index;
        }
        Move bestMove = Move.NullMove;
        for(int i = 0; i < moves.Length; i++) {
            if(timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30) return 0;

            int ind = i;
            for(int j = i + 1; j < moves.Length; j++) {
                if(scores[j] > scores[ind]) ind = j;
            }
            (scores[i], scores[ind]) = (scores[ind], scores[i]);
            (moves[i], moves[ind]) = (moves[ind], moves[i]);

            Move move = moves[i];
            board.MakeMove(move);
            int score = -Search(board, timer, -beta, -alpha, depth - 1, ply + 1);
            board.UndoMove(move);
            if(score > best) {
                best = score;
                bestMove = move;
                if(ply == 0)
                    bestmoveRoot = move;
                if(score > alpha) {
                    alpha = score;
                    if(alpha >= beta) break;
                }
            }
        }
        if(!qsearch && moves.Length == 0) {
            if(board.IsInCheck()) return -30000 + ply;
            else return 0;
        }
        tt[key % entries] = new TTEntry(key, bestMove);
        return best;
    }
    public Move Think(Board board, Timer timer)
    {
        Move bestMove = Move.NullMove;
        for(int depth = 1; depth <= 50; depth++) {
            int score = Search(board, timer, -30000, 30000, depth, 0);
            if(timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 30)
                break;
            Console.WriteLine($"info depth {depth} score cp {score} time {timer.MillisecondsElapsedThisTurn} pv {bestmoveRoot} nodes {nodes} nps {nodes * 1000 / (ulong)timer.MillisecondsElapsedThisTurn}");
            bestMove = bestmoveRoot;
        }
        return bestMove;
    }
}