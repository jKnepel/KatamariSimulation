using System;
using UnityEngine;

namespace jKnepel.Katamari
{
	public class AttachablesSpawner : MonoBehaviour
	{
		#region attributes

		[SerializeField] private Transform	_attachableParent;
		[SerializeField] private Attachable _attachablePrefab;
		[SerializeField] private int		_numberOfAttachables = 50;
		[SerializeField] private float		_spawnOffset = 2.0f;

		#endregion

		#region lifecycle

		private void OnEnable()
		{
			int numberOfColumns = (int)Math.Ceiling(Mathf.Sqrt(_numberOfAttachables));
			int numberOfRows = (int)Math.Ceiling((float)_numberOfAttachables / numberOfColumns);
			int remainder = numberOfColumns * (numberOfRows - 1);
			float startX = -(((float)(numberOfColumns - 1)) / 2 * _spawnOffset);
			float startZ = -(((float)(numberOfRows - 1)) / 2 * _spawnOffset);

			int index = 0;
			for (int i = 0; i < numberOfColumns; i++)
			{
				for (int j = 0; j < numberOfRows; j++)
				{
					if (i == numberOfColumns - 1 && remainder + j >= _numberOfAttachables)
						return;

					float x = startX + i * _spawnOffset;
					float z = startZ + j * _spawnOffset;
					Vector3 position = new(x, _attachablePrefab.transform.position.y, z);
					Attachable att = Instantiate(_attachablePrefab, position, _attachablePrefab.transform.rotation, _attachableParent);
					att.Initiate(index);
					index++;
				}
			}
		}

		#endregion
	}
}
