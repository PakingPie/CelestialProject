using UnityEngine;

public class WeaponBase : MonoBehaviour
{
    public Transform Targeted;
    [Tooltip("The range within which the gun can target enemies.")]
    public Vector2 ActiveRange = new Vector2(5f, 500f);
}