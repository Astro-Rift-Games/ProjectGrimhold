using Fusion;
using System.Collections;
using UnityEngine;

public class DartTrap : BaseTrap
{
    [SerializeField] Vector2 _direction;
    [SerializeField] GameObject _dartPrefab;
    [SerializeField] int _dartsAmount;
    [SerializeField] float _cooldown;
    [SerializeField] Transform _refPoint;

    protected override void Activate()
    {
        base.Activate();
        StartCoroutine(ShootDarts());
    }


    private IEnumerator ShootDarts()
    {
        //TO-DO: Add Animation
        for (int n = 0; n < _dartsAmount; n++)
        {
            //TO-DO: Add SFX
            ShootDart();
            yield return new WaitForSeconds(_cooldown);
        }
        yield return null;
    }

    private void ShootDart()
    {
        //TO-DO: Instantiate networked projectile via Runner

        Debug.Log("Dart shot");
    }

}
