namespace ChessChallenge.API
{
	using ChessChallenge.Application.APIHelpers;
	using ChessChallenge.Chess;
	using System;
	using System.Collections.Generic;

	public sealed class Board
	{
		readonly Chess.Board board;
		readonly APIMoveGen moveGen;

		readonly HashSet<ulong> repetitionHistory;
		readonly PieceList[] allPieceLists;
		readonly PieceList[] validPieceLists;

		Move[] cachedLegalMoves;
		bool hasCachedMoves;
		Move[] cachedLegalCaptureMoves;
		bool hasCachedCaptureMoves;

		/// <summary>
		/// Create a new board. Note: this should not be used in the challenge,
		/// use the board provided in the Think method instead.
		/// </summary>
		public Board(Chess.Board board)
		{
			this.board = board;
			moveGen = new APIMoveGen();
			cachedLegalMoves = Array.Empty<Move>();
			cachedLegalCaptureMoves = Array.Empty<Move>();

			// Init piece lists
			List<PieceList> validPieceLists = new();
			allPieceLists = new PieceList[board.pieceLists.Length];
			for (int i = 0; i < board.pieceLists.Length; i++)
			{
				if (board.pieceLists[i] != null)
				{
					allPieceLists[i] = new PieceList(board.pieceLists[i], this, i);
					validPieceLists.Add(allPieceLists[i]);
				}
			}
			this.validPieceLists = validPieceLists.ToArray();

			// Init rep history
			repetitionHistory = new HashSet<ulong>(board.RepetitionPositionHistory);
			repetitionHistory.Remove(board.ZobristKey);
		}

		/// <summary>
		/// Updates the board state with the given move.
		/// The move is assumed to be legal, and may result in errors if it is not.
		/// Can be undone with the UndoMove method.
		/// </summary>
		public void MakeMove(Move move)
		{
			hasCachedMoves = false;
			hasCachedCaptureMoves = false;
			if (!move.IsNull)
			{
				repetitionHistory.Add(board.ZobristKey);
				board.MakeMove(new Chess.Move(move.RawValue), inSearch: true);
			}
		}

		/// <summary>
		/// Undo a move that was made with the MakeMove method
		/// </summary>
		public void UndoMove(Move move)
		{
			hasCachedMoves = false;
			hasCachedCaptureMoves = false;
			if (!move.IsNull)
			{
				board.UndoMove(new Chess.Move(move.RawValue), inSearch: true);
				repetitionHistory.Remove(board.ZobristKey);
			}
		}

		/// <summary>
		/// Try skip the current turn
		/// This will fail and return false if in check
		/// Note: skipping a turn is not allowed in the game, but it can be used as a search technique
		/// </summary>
		public bool TrySkipTurn()
		{
			if (IsInCheck())
			{
				return false;
			}
			hasCachedMoves = false;
			hasCachedCaptureMoves = false;
			board.MakeNullMove();
			return true;
		}

		/// <summary>
		/// Undo a turn that was succesfully skipped with the TrySkipTurn method
		/// </summary>
		public void UndoSkipTurn()
		{
			hasCachedMoves = false;
			hasCachedCaptureMoves = false;
			board.UnmakeNullMove();
		}

		/// <summary>
		/// Gets an array of the legal moves in the current position.
		/// Can choose to get only capture moves with the optional 'capturesOnly' parameter.
		/// </summary>
		public Move[] GetLegalMoves(bool capturesOnly = false)
		{
			if (capturesOnly)
			{
				return GetLegalCaptureMoves();
			}

			if (!hasCachedMoves)
			{
				cachedLegalMoves = moveGen.GenerateMoves(board, includeQuietMoves: true);
				hasCachedMoves = true;
			}

			return cachedLegalMoves;
		}


		Move[] GetLegalCaptureMoves()
		{
			if (!hasCachedCaptureMoves)
			{
				cachedLegalCaptureMoves = moveGen.GenerateMoves(board, includeQuietMoves: false);
				hasCachedCaptureMoves = true;
			}
			return cachedLegalCaptureMoves;
		}

		/// <summary>
		/// Test if the player to move is in check in the current position.
		/// </summary>
		public bool IsInCheck() => board.IsInCheck();

		/// <summary>
		/// Test if the current position is checkmate
		/// </summary>
		public bool IsInCheckmate() => IsInCheck() && GetLegalMoves().Length == 0;

		/// <summary>
		/// Test if the current position is a draw due stalemate,
		/// 3-fold repetition, insufficient material, or 50-move rule.
		/// </summary>
		public bool IsDraw()
		{
			return IsFiftyMoveDraw() || Arbiter.InsufficentMaterial(board) || IsInStalemate() || IsRepetition();

			bool IsInStalemate() => !IsInCheck() && GetLegalMoves().Length == 0;
			bool IsFiftyMoveDraw() => board.currentGameState.fiftyMoveCounter >= 100;
			bool IsRepetition() => repetitionHistory.Contains(board.ZobristKey);
		}

		/// <summary>
		/// Does the given player still have the right to castle kingside?
		/// Note that having the right to castle doesn't necessarily mean castling is legal right now
		/// (for example, a piece might be in the way, or player might be in check, etc).
		/// </summary>
		public bool HasKingsideCastleRight(bool white) => board.currentGameState.HasKingsideCastleRight(white);

		/// <summary>
		/// Does the given player still have the right to castle queenside?
		/// Note that having the right to castle doesn't necessarily mean castling is legal right now
		/// (for example, a piece might be in the way, or player might be in check, etc).
		/// </summary>
		public bool HasQueensideCastleRight(bool white) => board.currentGameState.HasQueensideCastleRight(white);

		/// <summary>
		/// Gets the square that the king (of the given colour) is currently on.
		/// </summary>
		public Square GetKingSquare(bool white)
		{
			int colIndex = white ? Chess.Board.WhiteIndex : Chess.Board.BlackIndex;
			return new Square(board.KingSquare[colIndex]);
		}

		/// <summary>
		/// Gets the piece on the given square. If the square is empty, the piece will have a PieceType of None.
		/// </summary>
		public Piece GetPiece(Square square)
        {
            int p = board.Square[square.Index];
            bool white = PieceHelper.IsWhite(p);
            return new Piece((PieceType)PieceHelper.PieceType(p), white, square);
        }

        /// <summary>
        /// Gets a list of pieces of the given type and colour
        /// </summary>
        public PieceList GetPieceList(PieceType pieceType, bool white)
		{
			return allPieceLists[PieceHelper.MakePiece((int)pieceType, white)];
		}
		/// <summary>
		/// Gets an array of all the piece lists. In order these are:
		/// Pawns(white), Knights (white), Bishops (white), Rooks (white), Queens (white), King (white),
		/// Pawns (black), Knights (black), Bishops (black), Rooks (black), Queens (black), King (black)
		/// </summary>
		public PieceList[] GetAllPieceLists()
		{
			return validPieceLists;
		}
		
		/// <summary>
		/// Is the given square attacked by the opponent?
		/// (opponent being whichever player doesn't currently have the right to move)
		/// </summary>
		public bool SquareIsAttackedByOpponent(Square square)
		{
			if (!hasCachedMoves)
			{
				GetLegalMoves();
			}
			return BitboardHelper.SquareIsSet(moveGen.opponentAttackMap, square);
		}


		/// <summary>
		/// FEN representation of the current position
		/// </summary>
		public string GetFenString() => FenUtility.CurrentFen(board);

		/// <summary>
		/// 64-bit number where each bit that is set to 1 represents a
		/// square that contains a piece of the given type and colour.
		/// </summary>
		public ulong GetPieceBitboard(PieceType pieceType, bool white)
		{
			return board.pieceBitboards[PieceHelper.MakePiece((int)pieceType, white)];
		}
		/// <summary>
		/// 64-bit number where each bit that is set to 1 represents a square that contains any type of white piece.
		/// </summary>
		public ulong WhitePiecesBitboard => board.colourBitboards[Chess.Board.WhiteIndex];
		/// <summary>
		/// 64-bit number where each bit that is set to 1 represents a square that contains any type of black piece.
		/// </summary>
		public ulong BlackPiecesBitboard => board.colourBitboards[Chess.Board.BlackIndex];

		/// <summary>
		/// 64-bit number where each bit that is set to 1 represents a
		/// square that contains a piece of any type or colour.
		/// </summary>
		public ulong AllPiecesBitboard => board.allPiecesBitboard;


		public bool IsWhiteToMove => board.IsWhiteToMove;

		/// <summary>
		/// Number of ply (a single move by either white or black) played so far
		/// </summary>
		public int PlyCount => board.plyCount;

		/// <summary>
		/// 64-bit hash of the current position
		/// </summary>
		public ulong ZobristKey => board.ZobristKey;

        /// <summary>
        /// Creates a board from the given fen string. Please note that this is quite slow, and so it is advised
        /// to use the board given in the Think function, and update it using MakeMove and UndoMove instead.
        /// </summary>
        public static Board CreateBoardFromFEN(string fen)
        {
            Chess.Board boardCore = new Chess.Board();
            boardCore.LoadPosition(fen);
            return new Board(boardCore);
        }

    }
}