using UnityEngine;

public enum ObjectType { None, Bomb, Eye, Heal, TimeSlow }

public class ObjectIdentity : MonoBehaviour
{
    public ObjectType objectType;
}