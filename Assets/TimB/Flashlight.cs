using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Flashlight : MonoBehaviour
{
    private bool flashlightState, removeSpam;
    [SerializeField]
    private GameObject light;
    //[SerializeField]
    //private AudioSource sound;
    [SerializeField]
    KeyCode flashKey;
    
    // Start is called before the first frame update
    void Start()
    {
        flashlightState = false;
        removeSpam = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(flashKey))
        {
            if (removeSpam == false && flashlightState == true)
            {
                //sound.Play();
                flashlightState = !flashlightState;
                light.SetActive(false);
                removeSpam = true;
                StartCoroutine(RemoveSpam());
            }
            if (removeSpam == false && flashlightState == false)
            {
                //sound.Play();
                flashlightState = !flashlightState;
                light.SetActive(true);
                removeSpam = true;
                StartCoroutine(RemoveSpam());
            }
        }
    }
    IEnumerator RemoveSpam()
    {
        yield return new WaitForSeconds(0.2f);
        removeSpam = false;
    }
}