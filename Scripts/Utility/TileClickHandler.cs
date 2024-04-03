using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileClickHandler : MonoBehaviour
{
    public event System.Action<TileClickHandler> OnTileClicked;

    private void OnMouseDown()
    {
        if (OnTileClicked != null)
        {
            OnTileClicked(this);
        }
    }
}