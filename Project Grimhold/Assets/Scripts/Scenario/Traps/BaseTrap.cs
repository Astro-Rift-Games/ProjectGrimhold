using Fusion;
using System.Collections;
using UnityEngine;

public class BaseTrap : MonoBehaviour
{
    [SerializeField] TrapInfo trapInfo;
    [SerializeField] SpriteRenderer spriteRenderer;
    [Networked]
    private float _lastActivation { get; set; }

    protected virtual void Awake()
    {
        trapInfo.SetState(TrapState.Ready);
        _lastActivation = Time.time;
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (trapInfo == null) return;
        if (!trapInfo.IsReady) return;

        StartCoroutine(ActionTrap());
    }

    protected virtual IEnumerator ActionTrap()
    {
        Prepare();
        yield return new WaitForSeconds(trapInfo.activationTime);
        Activate();
        yield return new WaitForSeconds(trapInfo.resetTime);
        Deactivate();
        yield return new WaitForSeconds(trapInfo.cooldown);
        ResetTrap();
    }

    protected virtual void Prepare()
    {
        if (trapInfo == null) return;
        trapInfo.SetState(TrapState.Telegraphing);

        //TO-DO: Add animation
        //TO-DO: Add SFX
        spriteRenderer.color = Color.yellow;
    }

    protected virtual void Activate()
    {
        if (trapInfo == null) return;
        trapInfo.SetState(TrapState.Active);

        //TO-DO: Add animation
        //TO-DO: Add SFX
        spriteRenderer.color = Color.red;
    }

    protected virtual void Deactivate()
    {
        if (trapInfo == null) return;
        _lastActivation = Time.time;
        trapInfo.SetState(TrapState.InCooldown);

        //TO-DO: Add animation
        //TO-DO: Add SFX
        spriteRenderer.color = Color.gray3;
    }

    protected virtual void ResetTrap()
    {
        if (trapInfo == null) return;
        trapInfo.SetState(TrapState.Ready);

        //TO-DO: Add animation
        //TO-DO: Add SFX
        spriteRenderer.color = Color.green;
    }
}
