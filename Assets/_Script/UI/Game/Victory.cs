using UnityEngine;

public class Victory : MonoBehaviour
{
    private string playerTag = "Player";

    private EndGame _endGameScript;

    private void Awake()
    {
        _endGameScript = Object.FindFirstObjectByType<EndGame>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            if (_endGameScript != null)
            {
                _endGameScript.Win();

                gameObject.GetComponent<Collider>().enabled = false;
            }
            else
            {
                Debug.LogError("Victory: script EndGame non trovato nella scena!");
            }
        }
    }
}