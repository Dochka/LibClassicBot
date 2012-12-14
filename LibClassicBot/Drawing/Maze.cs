﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using LibClassicBot.Drawing;

namespace LibClassicBot.Drawing
{
	/// <summary>
	/// Fills a rectangular area with the specified block type.
	/// </summary>
	public sealed class Maze : IDrawer
	{
		/// <summary>
		/// Gets the name of the current drawing command.
		/// </summary>
		public string Name {
			get { return _name; }
		}
		
		const string _name = "Maze";
		
		/// <summary>
		/// Executes the drawer on a separate thread. The bool is there if the user finds a need to stop the drawing.
		/// (This happens when CancelDrawer() is called.)
		/// </summary>
		public void Start(ClassicBot main, ref bool Aborted, Vector3I[] points, byte blocktype, ref int sleeptime)
		{
			Vector3I Coords = Vector3I.Min(points[0], points[1]);
			Vector3I MinVertex = Vector3I.Min(points[0], points[1]);
			Vector3I MaxVertex = Vector3I.Max(points[0], points[1]);
			vicMazeGen.cMaze maze = new vicMazeGen.cMaze();
			maze.GenerateMaze(64, 64, 0, 5);
			main.SendPositionPacket((short)Coords.X, (short)Coords.Y, (short)Coords.Z);
			IEnumerator<Vector3I> coordEnumerator = maze.Get(128, 128, 2);
			while(coordEnumerator.MoveNext())
			{
				if (Aborted == true) { return; }
				Thread.Sleep(sleeptime);
				Coords = coordEnumerator.Current;
				Console.WriteLine(Coords);
				main.SendPositionPacket((short)Coords.X, (short)Coords.Y, (short)Coords.Z);
				main.SendBlockPacket((short)Coords.X, (short)Coords.Y, (short)Coords.Z, 1, blocktype);
			}
			main.SetDrawerToNull();
		}
	}
}

//http://www.ii.uni.wroc.pl/~wzychla/maze_en.html
// Wiktor Zychla, started 19.VII.2002
// v.0.02 - added smoothness
namespace vicMazeGen
{
	public class cMaze
	{
		private class FastStack<T>
		{
			private List<T> tStack;
			public void Push(T item) { tStack.Add(item); }
			public T Pop() {
				if ( tStack.Count > 0 )
				{
					T val = tStack[ tStack.Count - 1 ];
					tStack.RemoveAt ( tStack.Count - 1 );
					return val;
				}
				else return default(T);
			}
			internal FastStack() { tStack = new List<T>(); }
		}
		private enum Direction
		{
			N = 1,
			W = 2
		}
		public int MazeSizeX;
		public int MazeSizeY;
		
		private FastStack<cMazeState> stateStack;
		private Random r;
		private int []  maze_base;
		private byte[,] maze_data;
		private int iSmooth;

		#region Generating

		/// <summary>
		/// Just do some initialization and call the main routine,
		/// that is analyze_cell()
		/// </summary>
		/// <param name="sizeX"></param>
		/// <param name="sizeY"></param>
		/// <param name="seed"></param>
		public void GenerateMaze( int sizeX, int sizeY, int seed, int smoothness )
		{
			iSmooth = smoothness;
			MazeSizeX  = sizeX;
			MazeSizeY  = sizeY;
			maze_base = new int[MazeSizeX * MazeSizeY];
			maze_data = new Byte[MazeSizeX, MazeSizeY];

			stateStack = new FastStack<cMazeState>();
			r = new Random ( seed );

			MazeInit( r );
			
			cMazeState s = new cMazeState(r.Next() % MazeSizeX, r.Next() % MazeSizeY, 0);
			analyze_cell( s, r );
		}

		/// <summary>
		/// This is the main routine.
		/// The algorithm is pretty simple and very efficient.
		/// At the beginnins there are walls everywhere.
		/// 
		/// The algorithm walks around the maze choosing the
		/// random path at every step and looks if it can
		/// remove the wall between the cells.
		/// 
		/// The test that allows to remove the wall is also really
		/// simple: if the two cells are already connected then
		/// the wall is not allowed.
		/// 
		/// The only trick is the check whether the two cells are
		/// connected. To answer this question, the algorithm
		/// keeps the chains of connected walls. It works like this:
		/// - each chain consist of pointers to consecutive cells
		///   in the chain, last pointer is -1 and the cell with
		///   such value in maze_base is called base_cell of a cell.
		/// - at the beginning there are no chains, looking
		///   at maze_base[cellindex] you can find the value of -1
		/// - when two chains are merged, the pointer of the base cell
		///   of one of chains is changed so that it points to the
		///   base_cell of the other chain.
		///
		/// I've read about a similar trick by Tarjan but it looked
		/// much complicated ( or maybe I didn't understand it well ).
		/// Nevertheless, my code works really fast! Try to beat it.
		/// </summary>
		/// <param name="s"></param>
		/// <param name="r"></param>
		void analyze_cell( cMazeState state, Random r )
		{
			bool bEnd = false, found;
			int indexSrc, indexDest, tDir=0, prevDir=0;

			while (true)
			{
				if ( state.dir == 15 )
				{
					while ( state.dir == 15 )
					{
						state = (cMazeState)stateStack.Pop();
						if ( state == null )
						{
							bEnd = true;
							break;
						}
					}
					if ( bEnd == true ) break;
				}
				else
				{
					do
					{
						prevDir = tDir;
						tDir = (int)Math.Pow( 2, r.Next() % 4 );
						
						if ( (r.Next() % 32) < iSmooth )
							if ( (state.dir & prevDir) == 0 )
								tDir = prevDir;

						if ( (state.dir & tDir) != 0 )
							found = true;
						else
							found = false;
					} while ( found == true && state.dir!=15 );
					
					state.dir |= tDir;
					
					indexSrc  = cell_index( state.x, state.y );
					
					// direction W
					if ( tDir == 1 && state.x > 0 )
					{
						indexDest = cell_index( state.x-1, state.y );
						if ( base_cell( indexSrc ) != base_cell ( indexDest ) )
						{
							merge( indexSrc, indexDest );
							maze_data[state.x, state.y] |= (byte)Direction.W;
							
							stateStack.Push ( new cMazeState(state) );
							state.x -= 1;state.dir = 0;
						}
					}
					
					// direction E
					if ( tDir == 2 && state.x < MazeSizeX-1 )
					{
						indexDest = cell_index( state.x+1, state.y );
						if ( base_cell( indexSrc ) != base_cell ( indexDest ) )
						{
							merge( indexSrc, indexDest );
							maze_data[state.x+1, state.y] |= (byte)Direction.W;

							stateStack.Push ( new cMazeState(state) );
							state.x += 1; state.dir = 0;
						}
					}
					
					// direction N
					if ( tDir == 4 && state.y > 0 )
					{
						indexDest = cell_index( state.x, state.y-1 );
						if ( base_cell( indexSrc ) != base_cell ( indexDest ) )
						{
							merge( indexSrc, indexDest );
							maze_data[state.x, state.y] |= (byte)Direction.N;

							stateStack.Push ( new cMazeState(state) );
							state.y -= 1;state.dir = 0;
						}
					}
					
					// direction S
					if ( tDir == 8 && state.y < MazeSizeY-1 )
					{
						indexDest = cell_index( state.x, state.y+1 );
						if ( base_cell( indexSrc ) != base_cell ( indexDest ) )
						{
							merge( indexSrc, indexDest );
							maze_data[state.x, state.y+1] |= (byte)Direction.N;

							stateStack.Push ( new cMazeState(state) );
							state.y += 1;state.dir = 0;
						}
					}
				} // else
			} // while
		} // function

		#endregion
		#region Bitmap
		public IEnumerator<Vector3I> Get(int xSize, int ySize, int z)
		{
			yield return new Vector3I(0, 0, z + 1);
			yield return new Vector3I(xSize, ySize, z + 1);
			int xScaledSize = xSize / MazeSizeX;
			int yScaledSize = ySize / MazeSizeY;

			for ( int i = 0; i < MazeSizeX; i++ )
				for ( int j = 0; j < MazeSizeY; j++ )
			{
				// inspect the maze
				if ( (maze_data[i, j] & (byte)Direction.N) == 0 )
				{
					for(int x = xScaledSize * i; x < xScaledSize * (i + 1); x++)
					{
						yield return new Vector3I(x, yScaledSize * j, z);
					}
				}
				if ( (maze_data[i, j] & (byte)Direction.W) == 0 )
				{
					for(int y = yScaledSize * j; y < yScaledSize * (j + 1); y++)
					{
						yield return new Vector3I(xScaledSize * i, y, z);
					}
				}
			}
			for(int xStart = 0; xStart < xSize; xStart++) { yield return new Vector3I(xStart, 0, z); }
			for(int yStart = 0; yStart < ySize; yStart++) { yield return new Vector3I(0, yStart, z); }
			for(int xEnd = 0; xEnd < xSize; xEnd++) { yield return new Vector3I(xEnd, ySize, z); }
			for(int yEnd = 0; yEnd < ySize; yEnd++) { yield return new Vector3I(xSize, yEnd, z); }
			/*for(int xStart = 0; xStart < xScaledSize; xStart++)
			{
				yield return new Vector3I(xStart, 0, z + 1);
			}
			for(int yStart = 0; yStart < yScaledSize; yStart++)
			{
				yield return new Vector3I(0, yStart, z + 1);
			}
			for(int xEnd = xSize - xScaledSize; xEnd < xSize; xEnd++)
			{
				yield return new Vector3I(xEnd, ySize, z + 1);
			}
			for(int yEnd = ySize - yScaledSize; yEnd < ySize; yEnd++)
			{
				yield return new Vector3I(xSize, yEnd, z + 1);
			}*/
		}
		#endregion
		#region Cell functions
		int cell_index( int x, int y )
		{
			return MazeSizeX * y + x;
		}
		int base_cell( int tIndex )
		{
			int index = tIndex;
			while ( maze_base[ index ] >= 0 )
			{
				index = maze_base[ index ];
			}
			return index;
		}
		void merge( int index1, int index2 )
		{
			// merge both lists
			int base1 = base_cell( index1 );
			int base2 = base_cell( index2 );
			maze_base[ base2 ] = base1;
		}
		#endregion
		#region MazeInit
		void MazeInit( Random r )
		{
			int i, j;
			
			// maze data
			for (i=0; i<MazeSizeX; i++)
				for (j=0; j<MazeSizeY; j++)
			{
				maze_base [cell_index(i, j)] = -1;
				maze_data [i, j] = 0;
			}
		}


		#endregion
	}

	/// <summary>
	/// A single state of maze iteration.
	/// </summary>
	public class cMazeState
	{
		public int x, y, dir;
		public cMazeState( int tx, int ty, int td ) { x=tx; y=ty; dir = td; }
		public cMazeState( cMazeState s ) { x=s.x; y=s.y; dir=s.dir; }
	}
}