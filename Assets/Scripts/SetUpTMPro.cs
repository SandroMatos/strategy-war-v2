using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Threading;


public class SetUpTMPro : MonoBehaviour
{

    private TextMeshProUGUI textddd;
    private float timer = 0f;
    private float cooldown = 5f;





    // Start is called before the first frame update
    void Start()
    {
        textddd = GetComponent<TextMeshProUGUI>();
        timer = cooldown;
    }
    

    // Update is called once per frame
    void Update()
    {
        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }
        else
        {
            textddd.text = "Hello, World!";
        }
    }
}
