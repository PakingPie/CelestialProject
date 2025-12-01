using UnityEngine;
using System.Collections.Generic;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance { get; private set; }
    
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private int _initialPoolSize = 100;
    
    private Queue<GameObject> _pool = new Queue<GameObject>();
    
    void Awake()
    {
        Instance = this;
        
        // Pre-spawn bullets
        for (int i = 0; i < _initialPoolSize; i++)
        {
            GameObject bullet = Instantiate(_bulletPrefab, transform);
            bullet.SetActive(false);
            _pool.Enqueue(bullet);
        }
    }
    
    public GameObject Get(Vector3 position, Quaternion rotation)
    {
        GameObject bullet;
        
        if (_pool.Count > 0)
        {
            bullet = _pool.Dequeue();
            bullet.transform.SetPositionAndRotation(position, rotation);
            bullet.SetActive(true);
        }
        else
        {
            bullet = Instantiate(_bulletPrefab, position, rotation);
        }
        
        return bullet;
    }
    
    public void Return(GameObject bullet)
    {
        bullet.SetActive(false);
        bullet.transform.SetParent(transform);
        _pool.Enqueue(bullet);
    }
}