using UnityEngine;

public class CharacterController : MonoBehaviour
{
    [Header("Movements Params")]
    [SerializeField] private float dashSpeed = 50f;
    [SerializeField] private float runSpeed = 25f;

    [Header("Life Params")]
    [SerializeField] private float maxLife = 100f;
    [SerializeField] private float currentLife = 100f;
    public bool isDead { get; private set; }

    [Header("Damage Params")]
    [SerializeField] private float invunerableTime = 0.5f;


    private AnimationController anim;

    public float MaxLife { get => maxLife; set => maxLife = value; }
    public float RunSpeed { get => runSpeed; }
    public float DashSpeed { get => dashSpeed; }
    public float InvunerableTime { get => invunerableTime; }

    public float CurrentLife => currentLife;

    private void Awake()
    {

        anim = GetComponentInChildren<AnimationController>();

        // Garantia básica pra não começar com vida inválida
        if (maxLife <= 0f) maxLife = 1f;
        currentLife = Mathf.Clamp(currentLife, 0f, maxLife);
        isDead = currentLife <= 0f;
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        if (amount <= 0f) return;

        currentLife = Mathf.Min(currentLife + amount, maxLife);
    }

    public void TakeDamage(float amount)
    {
        if (isDead) return;
        if (amount <= 0f) return;

        currentLife = Mathf.Max(currentLife - amount, 0f);

        if (currentLife <= 0f)
            Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        // Se o seu AnimationController tiver um método/trigger de morte, chama aqui:
        // anim?.PlayDeath();
    }
}