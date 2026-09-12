using UnityEngine;

public class RobustTester : MonoBehaviour {
    public float interval = 2.0f;
    private Animator anim;
    private float nextTime;
    
    // Explicitly define parameters to set
    private string[] triggers = { "AttackTrigger", "HitTrigger" };
    private bool combat = false;

    void Start() {
        Time.timeScale = 1.0f;
    }

    void Update() {
        if (!anim) anim = GetComponentInChildren<Animator>();
        if (!anim) return;

        if (Time.time > nextTime) {
            nextTime = Time.time + interval;
            
            // Cycle combat mode
            combat = !combat;
            anim.SetBool("IsInCombat", combat);
            
            // Random Speed
            anim.SetFloat("Speed", Random.value > 0.5f ? 0.5f : 0f);
            
            // Fire Trigger
            string t = triggers[Random.Range(0, triggers.Length)];
            anim.SetTrigger(t);
            
            Debug.Log($"[RobustTester] Mode Change: Combat={combat}, Trigger={t}");
        }
    }
}