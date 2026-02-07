using UnityEngine;

public class DoorScript : MonoBehaviour
{
    void OnMouseDown()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<playerScript>().TryOpenDoor();
    }
}
