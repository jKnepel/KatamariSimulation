using System;
using UnityEngine;

namespace jKnepel.Katamari
{
	public class AttachablesManager : MonoBehaviour
	{
		#region attributes

		[SerializeField] private Transform	_attachableParent;
		[SerializeField] private Attachable _attachablePrefab;
		[SerializeField] private int		_numberOfAttachables = 50;
		[SerializeField] private float		_spawnOffset = 2.0f;

		public Attachable[] Attachables;

		#endregion

		#region lifecycle

		private void Awake()
		{
			if (_attachableParent == null)
				_attachableParent = transform;

			Attachables = new Attachable[_numberOfAttachables];
			int numberOfColumns = (int)Math.Ceiling(Mathf.Sqrt(_numberOfAttachables));
			int numberOfRows = (int)Math.Ceiling((float)_numberOfAttachables / numberOfColumns);
			int remainder = _numberOfAttachables % numberOfRows;
			float startX = -(((float)(numberOfColumns - 1)) / 2 * _spawnOffset);
			float startZ = -(((float)(numberOfRows - 1)) / 2 * _spawnOffset);

			int index = 0;
			for (int i = 0; i < numberOfColumns; i++)
			{
				for (int j = 0; j < numberOfRows; j++)
				{
					if (remainder > 0 && i == numberOfColumns - 1 && j >= remainder)
						return;

					float x = startX + i * _spawnOffset;
					float z = startZ + j * _spawnOffset;
					Vector3 position = new(x, _attachablePrefab.transform.position.y, z);
					Attachable att = Instantiate(_attachablePrefab, position, _attachablePrefab.transform.rotation, _attachableParent);
					att.gameObject.name = $"Attachable#{index}";
					Attachables[index++] = att;
				}
			}
		}

		#endregion
	}
}
