using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Lianliankan : MonoBehaviour
{
	private enum GridEnterDirection
	{
		UP = 0,
		DOWN = 1,
		LEFT = 2,
		RIGHT = 3,
	}
	private static readonly GridEnterDirection[] gridEnterDirections = new GridEnterDirection[4]
	{
		GridEnterDirection.UP,
		GridEnterDirection.DOWN,
		GridEnterDirection.LEFT,
		GridEnterDirection.RIGHT,
	};

	private struct GridUnit
	{
		public GridObject GridObj;

		public int Num;

		public bool Revealed;

		public void SetAsInSelecting(bool s)
		{
			if (GridObj is not null)
			{
				GridObj.BgImage.color = s ? Color.gray : Color.white;
			}
		}
		public int x, y;
		public int hCost;
		public int[] gCost;
		public int[] gCostDir;

		public void InitNode(int x, int y)
		{
			this.x = x;
			this.y = y;
			Revealed = true;
		}
		public int GetGCostDirByDir(GridEnterDirection direction) => gCostDir[((int)direction)];
		public int GetGCostByDir(GridEnterDirection direction) => gCost[((int)direction)];

		public (GridEnterDirection, int, int) GetBestGDirAndCost()
		{
			(GridEnterDirection, int, int) cost = (gridEnterDirections[0], gCostDir[0], gCost[0]);
			for (int i = 1; i < 4; i++)
			{
				if (gCostDir[i] < cost.Item2 && gCost[i] < cost.Item3)
				{
					cost = (gridEnterDirections[i], gCostDir[i], gCost[i]);
				}
			}
			return cost;
		}
		public (int x, int y) GetNeighborByDirection(GridEnterDirection direction) => direction switch
		{
			GridEnterDirection.UP => (x - 1, y),
			GridEnterDirection.DOWN => (x + 1, y),
			GridEnterDirection.LEFT => (x, y - 1),
			GridEnterDirection.RIGHT => (x, y + 1),
			_ => (x - 1, y),
		};
		public (int x, int y) GetReverseNeighborByDirection(GridEnterDirection direction) => direction switch
		{
			GridEnterDirection.UP => (x + 1, y),
			GridEnterDirection.DOWN => (x - 1, y),
			GridEnterDirection.LEFT => (x, y + 1),
			GridEnterDirection.RIGHT => (x, y - 1),
			_ => (x + 1, y),
		};

		public bool IsBestDir(GridEnterDirection d)
		{
			foreach (var dir in gridEnterDirections)
			{
				if (GetGCostDirByDir(dir) < GetGCostDirByDir(d))
				{
					return false;
				}
				else
				{
					if (GetGCostDirByDir(dir) == GetGCostDirByDir(d))
					{
						if (GetGCostByDir(dir) < GetGCostByDir(d))
						{
							return false;
						}
					}
				}
			}
			return true;
		}
	}

	private class GridObject
	{
		public GameObject Obj;
		public Image BgImage;
		public Image IconImage;
		public Button Button;
	}

	[SerializeField] private Sprite[] iconSheet;
	[SerializeField] private Transform gridContent;
	[SerializeField] private GameObject gridPrefab;
	[SerializeField] private Transform lineContent;
	[SerializeField] private GameObject linePrefab;

	[SerializeField] private int tableWidth = 12;
	[SerializeField] private int tableHeight = 12;

	private GridUnit[,] gridTable;
	private Stack<Transform> linePool = new Stack<Transform>();
	private Queue<Transform> inShowingLine = new Queue<Transform>();
	private (int x, int y) inSelect;

	private List<GridObject> gridObjects = new List<GridObject>();
	private (int x, int y) InSelect
	{
		get => inSelect;
		set
		{
			if (inSelect.x != -1)
			{
				gridTable[inSelect.x, inSelect.y].SetAsInSelecting(false);
			}
			inSelect = value;
			if (inSelect.x != -1)
			{
				gridTable[inSelect.x, inSelect.y].SetAsInSelecting(true);
			}
		}
	}
	[ContextMenu("ResetTable")]
	public void ResetTable()
	{
		gridTable = new GridUnit[tableHeight + 2, tableWidth + 2];
		for (int i = 0; i < tableHeight; i++)
		{
			for (int j = 0; j < tableWidth; j++)
			{
				gridTable[i + 1, j + 1].GridObj = i * tableWidth + j >= gridObjects.Count ? CreateNewGridObject() : gridObjects[i * tableWidth + j];
				gridTable[i + 1, j + 1].GridObj.Obj.transform.localPosition = new Vector3(j * 50, i * 50, 0);
			}
		}

		for (int i = 0; i < tableHeight + 2; i++)
		{
			for (int j = 0; j < tableWidth + 2; j++)
			{
				gridTable[i, j].InitNode(i, j);
			}
		}

		int[] numList = new int[tableHeight * tableWidth];

		for (int i = 0; i < tableWidth * tableHeight; i++)
		{
			numList[i] = i / 4;
			var r = Random.Range(0, i);
			(numList[i], numList[r]) = (numList[r], numList[i]);
		}
		for (int i = 0, n; i < tableHeight; i++)
		{
			for (int j = 0; j < tableWidth; j++)
			{
				n = numList[i * tableWidth + j];
				gridTable[i + 1, j + 1].Num = n;
				gridTable[i + 1, j + 1].Revealed = false;
				gridTable[i + 1, j + 1].GridObj.IconImage.sprite = iconSheet[n];
				gridTable[i + 1, j + 1].GridObj.Obj.SetActive(true);
			}
		}
		InSelect = (-1, -1);
		HideAllLine();
	}
	private GridObject CreateNewGridObject()
	{
		var go = new GridObject();
		go.Obj = Instantiate(gridPrefab, gridContent);
		go.BgImage = go.Obj.GetComponent<Image>();
		go.IconImage = go.Obj.transform.GetChild(0).GetComponent<Image>();
		go.Button = go.Obj.GetComponent<Button>();
		var idx = gridObjects.Count;
		go.Button.onClick.AddListener(() => OnBtnClick(idx));
		gridObjects.Add(go);
		go.Obj.SetActive(true);
		return go;
	}

	private bool IsPositionLegal((int x, int y) pos)
	{
		if (pos.x < 0 || pos.y < 0)
		{
			return false;
		}
		if (pos.x >= tableHeight + 2 || pos.y >= tableWidth + 2)
		{
			return false;
		}
		return true;
	}
	private bool IsPositionPassable((int x, int y) pos)
	{
		return gridTable[pos.x, pos.y].Revealed;
	}

	private void ResetNode((int x, int y) pos)
	{
		var grid = gridTable[pos.x, pos.y];
		grid.gCostDir = new int[4] { int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue };
		grid.gCost = new int[4] { int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue };
		gridTable[pos.x, pos.y] = grid;
	}
	private void UpdateNode((int x, int y) pos, GridEnterDirection dir, int costDir, int cost, (int x, int y) t)
	{
		var grid = gridTable[pos.x, pos.y];
		grid.gCostDir[((int)dir)] = costDir;
		grid.gCost[((int)dir)] = cost;
		grid.hCost = Mathf.Abs(t.x - pos.x) + Mathf.Abs(t.y - pos.y);
		gridTable[pos.x, pos.y] = grid;
	}

	private void RevealNode((int x, int y) pos)
	{
		var grid = gridTable[pos.x, pos.y];
		grid.GridObj.Obj.SetActive(false);
		grid.Revealed = true;
		gridTable[pos.x, pos.y] = grid;
	}
	private GridUnit GetGrid((int x, int y) xy)
	{
		return gridTable[xy.x, xy.y];
	}
	private int GetNum((int x, int y) xy)
	{
		return gridTable[xy.x, xy.y].Num;
	}
	private void OnBtnClick(int selIdx)
	{
		(int x, int y) sel = (selIdx / tableWidth + 1, selIdx % tableWidth + 1);
		if (InSelect.x == -1)
		{
			InSelect = sel;
			return;
		}
		if (InSelect == sel)
		{
			InSelect = (-1, -1);
			return;
		}
		if (GetNum(InSelect) == GetNum(sel))
		{
			Queue<(int x, int y)> path = AStarSearch(InSelect, sel);
			if (path == null)
			{
				Debug.Log("Find path faild");
			}
			else
			{
				Debug.Log(string.Join(",", path));
				RevealNode(sel);
				RevealNode(InSelect);
				DrawLine(path);
			}
			InSelect = (-1, -1);
		}
		else
		{
			InSelect = (-1, -1);
		}
	}
	void DrawLine(Queue<(int x, int y)> path)
	{
		(int x, int y) start;
		(int x, int y) end;
		int lineCount = 0;
		(int x, int y) dir;
		start = path.Dequeue();
		dir = TupleSubtract(path.Peek(), start);
		while (path.Count > 0)
		{

			end = path.Dequeue();

			if (path.Count == 0)
			{
				Draw();
			}
			else
			{
				if (dir != TupleSubtract(path.Peek(), end))
				{
					Draw();

					start = end;
					dir = TupleSubtract(path.Peek(), start);
				}
			}
		}

		StartCoroutine(HideLine(lineCount));
		void Draw()
		{
			Transform line = GetImage();
			var startPos = new Vector3(start.y * 50f + 25f, start.x * 50f + 25f, 0f);
			var endPos = new Vector3(end.y * 50f + 25f, end.x * 50f + 25f, 0f);
			line.position = startPos + (endPos - startPos) / 2f;
			(line as RectTransform).sizeDelta = new Vector2(Vector3.Distance(startPos, endPos), 10);
			line.rotation = Quaternion.Euler(0f, 0f, GetAngle(startPos, endPos));
			line.gameObject.SetActive(true);
			inShowingLine.Enqueue(line);
			lineCount++;
		}
	}
	private (int x, int y) TupleSubtract((int x, int y) t1, (int x, int y) t2) => (t1.x - t2.x, t1.y - t2.y);
	private IEnumerator HideLine(int n)
	{
		yield return new WaitForSeconds(1);
		for (int i = 0; i < n; i++)
		{
			RecycleImage(inShowingLine.Dequeue());
		}
	}

	private void HideAllLine()
	{
		while (inShowingLine.Count > 0)
		{
			RecycleImage(inShowingLine.Dequeue());
		}
	}
	private float GetAngle(Vector3 start, Vector3 end)
	{
		if (end.y - start.y > 0)
		{
			return Vector3.Angle(Vector3.right, end - start);
		}
		else
		{
			return -Vector3.Angle(Vector3.right, end - start);
		}
	}
	private Transform GetImage()
	{
		if (linePool.Count > 0)
		{
			return linePool.Pop();
		}
		return GameObject.Instantiate(linePrefab, lineContent).transform;
	}
	private void RecycleImage(Transform image)
	{
		image.gameObject.SetActive(false);
		linePool.Push(image);
	}
	private List<(int x, int y)> openList = new List<(int x, int y)>();
	private List<(int x, int y)> closeList = new List<(int x, int y)>();

	private Queue<(int x, int y)> AStarSearch((int x, int y) start, (int x, int y) targetPos)
	{
		openList.Clear();
		closeList.Clear();

		openList.Add(start);
		ResetNode(start);
		UpdateNode(start, GridEnterDirection.UP, 0, 0, targetPos);
		UpdateNode(start, GridEnterDirection.DOWN, 0, 0, targetPos);
		UpdateNode(start, GridEnterDirection.LEFT, 0, 0, targetPos);
		UpdateNode(start, GridEnterDirection.RIGHT, 0, 0, targetPos);
		var tarGu = gridTable[targetPos.x, targetPos.y];
		while (openList.Count > 0)
		{
			(int x, int y) minPos = AStarGetMinNode(openList);
			var minGu = gridTable[minPos.x, minPos.y];

			openList.Remove(minPos);
			closeList.Add(minPos);

			foreach (var direction in gridEnterDirections)
			{
				(int x, int y) guPos = gridTable[minPos.x, minPos.y].GetNeighborByDirection(direction);
				if (!IsPositionLegal(guPos))
				{
					continue;
				}
				var gu = gridTable[guPos.x, guPos.y];
				if (!IsPositionPassable(guPos))
				{
					if (gu.Num != tarGu.Num)
					{
						continue;
					}
				}
				if (openList.Contains(guPos))
				{
					var best = minGu.GetBestGDirAndCost();
					if (minPos == start || minGu.IsBestDir(direction))
					{
						if (best.Item2 <= gu.GetGCostDirByDir(direction) && best.Item3 + 1 <= gu.GetGCostByDir(direction))
						{
							UpdateNode(guPos, direction, best.Item2, best.Item3 + 1, targetPos);
						}
					}
					else
					{
						if (best.Item2 + 1 <= gu.GetGCostDirByDir(direction) && best.Item3 + 1 <= gu.GetGCostByDir(direction))
						{
							UpdateNode(guPos, direction, best.Item2 + 1, best.Item3 + 1, targetPos);
						}
					}
				}
				else
				{
					if (!closeList.Contains(guPos))
					{
						openList.Add(guPos);
						ResetNode(guPos);
						var best = minGu.GetBestGDirAndCost();
						UpdateNode(guPos, direction, minPos == start || minGu.IsBestDir(direction) ? best.Item2 : best.Item2 + 1, best.Item3 + 1, targetPos);
					}
				}
				gu = gridTable[guPos.x, guPos.y];
				if (guPos == targetPos && gu.GetBestGDirAndCost().Item2 < 3)
				{
					Queue<(int x, int y)> path = new Queue<(int x, int y)>();
					var dir = gu.GetBestGDirAndCost();
					path.Enqueue(guPos);
					while (true)
					{
						var pos = gu.GetReverseNeighborByDirection(dir.Item1);
						path.Enqueue(pos);
						if (pos == start)
						{
							break;
						}
						gu = GetGrid(pos);
						if (!gu.IsBestDir(dir.Item1))
						{
							dir = gu.GetBestGDirAndCost();
						}
					}
					return path;
				}
			}
		}
		return null;
	}
	private (int x, int y) AStarGetMinNode(List<(int x, int y)> l)
	{
		var grid = gridTable[l[0].x, l[0].y];
		var best = grid.GetBestGDirAndCost();
		var fc = best.Item3 + grid.hCost;
		(int x, int y) min = l[0];
		foreach ((int x, int y) gu in l)
		{
			grid = gridTable[gu.x, gu.y];
			var nbest = grid.GetBestGDirAndCost();
			var nfc = nbest.Item3 + grid.hCost;
			if (nbest.Item2 < best.Item2 || (nbest.Item2 == best.Item2 && nfc < fc))
			{
				best = nbest;
				fc = nfc;
				min = gu;
			}
		}
		return min;
	}
}
