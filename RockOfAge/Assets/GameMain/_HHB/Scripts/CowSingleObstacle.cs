using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class CowSingleObstacle : MoveObstacleBase
{
    [SerializeField]
    private Collider[] _colliders;
    private Transform cow;
    public bool isSticked;
    public int groundCount;
    private bool delayBool;

    protected override void Init()
    {
        base.Init();
        cow = GetComponent<Transform>();
        obstacleMeshFilter = GetComponent<MeshFilter>();
        obstacleRenderers = GetComponentsInChildren<Renderer>();
        obstacleRigidBody = GetComponent<Rigidbody>();
        _colliders = GetComponents<Collider>();
        foreach(var collider in _colliders)
        {
            if (collider.isTrigger)
            {

            }
            else
            {
                obstacleCollider = collider;
            }
        }
        currHealth = status.Health;
        isSticked = false;
        delayBool = false;
        //transform.parent = obstacleParent;
    }

    [PunRPC]
    public void SetActive()
    {
        if (audioSource != null)
        {
            audioSource.Play();
        }
        isSticked = true;
        delayBool = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isBuildComplete)
        {
            return;

        }
        if (!isSticked && collision.gameObject.layer == LayerMask.NameToLayer("Rock") && collision.collider.name=="RockObject")
        {
            Transform rock = collision.transform;
            photonView.TransferOwnership(rock.GetComponentInParent<PhotonView>().Owner);
            Destroy(obstacleRigidBody);
            photonView.RPC("SetActive", RpcTarget.All);
            Physics.IgnoreCollision(collision.collider, obstacleCollider);
            transform.localPosition -= (transform.position - rock.position) * .4f;
            cow.SetParent(rock);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isBuildComplete)
        {
            if (delayBool && isSticked && other.gameObject.layer == LayerMask.NameToLayer("Terrains"))
            {
                if (groundCount <= 1)
                {
                    StartCoroutine(DelayBool());
                }
                else
                {
                    cow.parent = null;
                    Invoke("DestroyPhotonViewObject", 1f);
                }
            }
        }
    }

    IEnumerator DelayBool()
    {
        groundCount++;
        delayBool = false;
        yield return new WaitForSeconds(1f);
        delayBool = true;
    }
}
