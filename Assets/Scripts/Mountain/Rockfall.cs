using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Farhad
{
    // The mountain answering bad strikes: pebbles and groaning as a warning, then a collapse that
    // buries Farhad under rock.
    public class Rockfall : MonoBehaviour
    {
        public MountainGrid mountain;
        public CameraFollow cameraFollow;
        public Sprite debrisSprite;
        public int sortingOrder = 15;

        [Header("Collapse")]
        public int debrisCount = 12;
        public float fallHeight = 3f;
        public float gravity = 30f;

        class Piece
        {
            public Transform transform;
            public float speed;
            public float stopY;
            public bool removeOnLanding;
            public bool landed;
        }

        readonly List<Piece> pieces = new();

        // severity: 0 = first strain, approaching 1 = the next bad strike brings the mountain down.
        public void Warn(Vector2 aboveHead, float severity)
        {
            cameraFollow.Shake(0.05f + 0.2f * severity, 0.3f + 0.5f * severity);
            int pebbles = 1 + Mathf.RoundToInt(severity * 5f);
            for (int i = 0; i < pebbles; i++)
            {
                var from = aboveHead + new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(0.5f, 1.5f));
                Spawn(from, Random.Range(0.1f, 0.22f), from.y - 3f, removeOnLanding: true);
            }
        }

        public void Collapse(FarhadController farhad) => StartCoroutine(CollapseRoutine(farhad));

        IEnumerator CollapseRoutine(FarhadController farhad)
        {
            farhad.sounds.PlayRockfall();
            cameraFollow.Shake(0.45f, 1.4f);

            Vector2 feet = farhad.transform.position + Vector3.down * 0.9f;
            var falling = new List<Piece>();
            for (int i = 0; i < debrisCount; i++)
            {
                var from = feet + new Vector2(Random.Range(-1.2f, 1.2f), fallHeight + Random.Range(0f, 2.5f));
                falling.Add(Spawn(from, Random.Range(0.35f, 0.65f), feet.y + Random.Range(0.2f, 1.6f), removeOnLanding: false));
            }

            while (falling.Exists(p => !p.landed)) yield return null;
            yield return new WaitForSeconds(0.15f);

            Bury(farhad);
            foreach (var p in falling)
            {
                pieces.Remove(p);
                Destroy(p.transform.gameObject);
            }
        }

        // Fill the space around Farhad with solid rock: the tunnel closes over him.
        void Bury(FarhadController farhad)
        {
            var center = mountain.rockMap.WorldToCell(farhad.transform.position);
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                var cell = center + new Vector3Int(x, y, 0);
                if (mountain.GetRock(cell) == RockType.Empty) mountain.rockMap.SetTile(cell, mountain.solidTile);
            }
            farhad.body.enabled = false;
        }

        Piece Spawn(Vector2 position, float size, float stopY, bool removeOnLanding)
        {
            var go = new GameObject("Debris");
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = debrisSprite;
            sr.sortingOrder = sortingOrder;

            var piece = new Piece { transform = go.transform, stopY = stopY, removeOnLanding = removeOnLanding };
            pieces.Add(piece);
            return piece;
        }

        void Update()
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var p = pieces[i];
                if (p.landed) continue;
                p.speed += gravity * Time.deltaTime;
                var pos = p.transform.position;
                pos.y -= p.speed * Time.deltaTime;
                if (pos.y <= p.stopY)
                {
                    pos.y = p.stopY;
                    p.landed = true;
                    if (p.removeOnLanding)
                    {
                        pieces.RemoveAt(i);
                        Destroy(p.transform.gameObject);
                        continue;
                    }
                }
                p.transform.position = pos;
            }
        }
    }
}
