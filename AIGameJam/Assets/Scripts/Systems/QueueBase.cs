using UnityEngine;

public class QueueBase<T>
{
    private class Node
    {
        public T obj;
        public Node next;
        public Node(T obj) => this.obj = obj;
    }

    private Node head;
    private Node tail;
    private int count;
    
    public int Count => count;
    public bool IsEmpty => count == 0;
    
    public void Enqueue(T item)
    {
        var node = new Node(item);
        if (tail == null)
        {
            head = node;
            tail = node;
        }
        else
        {
            tail.next = node;
            tail = node;
        }

        count++;
    }

    public T Dequeue()
    {
        T item = head.obj;
        head = head.next;
        count--;

        if (head == null)
        {
            tail = null;
        }

        return item;
    }

    public void Clear()
    {
        head = null;
        tail = null;
        count = 0;
    }
}
