using System;
using UnityEngine;

namespace jKnepel.Katamari
{
	public class NetworkObjectSpawner : MonoBehaviour
	{
		[SerializeField] private Transform _objectParent;
		[SerializeField] private NetworkObject _objectPrefab;
		[SerializeField] private int _numberOfObjects = 50;
		[SerializeField] private float _spawnDistance = 2.0f;

		private void Awake()
		{
			int numberOfColumns = (int)Math.Ceiling(Mathf.Sqrt(_numberOfObjects));
			int numberOfRows = (int)Math.Ceiling((float)_numberOfObjects / numberOfColumns);
			int remainder = _numberOfObjects % numberOfRows;
			float startX = -(((float)(numberOfColumns - 1)) / 2 * _spawnDistance);
			float startZ = -(((float)(numberOfRows - 1)) / 2 * _spawnDistance);

			int index = 0;
			for (int i = 0; i < numberOfColumns; i++)
			{
				for (int j = 0; j < numberOfRows; j++)
				{
					if (remainder > 0 && i == numberOfColumns - 1 && j >= remainder)
						return;

					float x = startX + i * _spawnDistance;
					float z = startZ + j * _spawnDistance;
					Vector3 position = new(x, _objectPrefab.transform.position.y, z);
					NetworkObject obj = Instantiate(_objectPrefab, position, _objectPrefab.transform.rotation, _objectParent);
					obj.gameObject.name = $"Object#{index}";
					obj.gameObject.SetActive(true);
				}
			}
		}
	}
}
