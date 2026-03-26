using UnityEngine;
using Pathfinding;

public class EnemyAI : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float speed = 200f;
    [SerializeField] private float nextWaypointDistance = 1f;

    private Path path;
    private int currentWaypoint = 0;
    private bool reachedEndOfPath = false;

    Seeker seeker;
    Rigidbody2D rb;

    void Start()
    {
        seeker = GetComponent<Seeker>();
        rb = GetComponent<Rigidbody2D>();
        InvokeRepeating("UpdatePath", 0f, 0.5f); // Update path every 0.5 seconds
    }

    void FixedUpdate()
    {
        if (path == null)
        {
            return; // No path to follow
        }


        if (currentWaypoint >= path.vectorPath.Count)
        {
            reachedEndOfPath = true;
            return; // Reached the end of the path
        }
        else
        {
            reachedEndOfPath = false;
        }

        Vector2 direction = ((Vector2)path.vectorPath[currentWaypoint] - (Vector2)rb.position).normalized;
        Vector2 force = direction * speed * Time.deltaTime;


        rb.AddForce(force);

        float distance = Vector2.Distance(rb.position, path.vectorPath[currentWaypoint]);

        if (distance < nextWaypointDistance)
        {
            currentWaypoint++;
        }

        if (force.x >= 0.01f)
        {
            transform.localScale = new Vector3(1, 1, 1); // Facing right
        }
        else if (force.x <= -0.01f)
        {
            transform.localScale = new Vector3(-1, 1, 1); // Facing left
        }
    }

    private void UpdatePath()
    {
        if (seeker.IsDone())
        {
            seeker.StartPath(rb.position, target.position, OnPathComplete);
        }
    }

    public void OnPathComplete(Path p)
    {
        if (!p.error)
        {
            path = p;
            currentWaypoint = 0;
        }
    }
}