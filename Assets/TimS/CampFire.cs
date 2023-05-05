using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CampFire : MonoBehaviour
{

    [SerializeField] private float burnoutValue;
    [SerializeField] private float fireHealth;
    // Start is called before the first frame update
    void Start()
    {
        fireHealth = 100;
        burnoutValue = 0.1f;
    }

    // Update is called once per frame
    void Update()
    {
        fireHealth -= burnoutValue * Time.deltaTime;

        //BURNOUT VALUE
        if(fireHealth <= 25)
        {
            burnoutValue = 0.2f;
        }
        else { burnoutValue = 0.1f; }

        if(fireHealth <= 0)
        {
            //END GAME
        }
    }
}
