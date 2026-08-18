using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PartnerLocomotion : MonoBehaviour
{
    public enum Action { None, Cook, Pet, Sit, IdleDog }      // → action rows 0..3

    [Header("Walk [8 dirs][6] rows S,SE,E,NE,N,NW,W,SW")] public Sprite[] walk = new Sprite[48];
    [Header("Action [4][6] Cook,Pet,Sit,IdleDog")]        public Sprite[] action = new Sprite[24];
    public float walkFps = 9f, actionFps = 6f, moveSpeed = 2.2f;
    public bool mirrorWestRows = false;                       // true if you only supplied rows 0..4
    public Action current = Action.None;

    SpriteRenderer _sr; int _dir; float _frameT, _distance; int _frame;
    void Awake(){ _sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        float dt = Time.deltaTime;
        if (current != Action.None) {
            _frameT += dt; if (_frameT >= 1f/actionFps) { _frameT = 0; _frame = (_frame+1)%6; }
            _sr.flipX = false; _sr.sprite = action[((int)current-1)*6 + _frame]; return;
        }
        Vector2 mv = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (mv.sqrMagnitude > 0.01f) {
            transform.position += (Vector3)(mv.normalized*moveSpeed*dt);
            _dir = DirFromVector(mv); _distance += moveSpeed*dt;
            SetWalk(_dir, Mathf.FloorToInt(_distance*(walkFps/moveSpeed))%6);
        } else SetWalk(_dir, 0);
    }
    void SetWalk(int dir, int f){
        int row = dir; bool flip = false;
        if (mirrorWestRows && dir >= 5) { row = new[]{0,0,0,0,0,3,2,1}[dir]; flip = true; }
        _sr.flipX = flip; _sr.sprite = walk[row*6 + f];
    }
    int DirFromVector(Vector2 v){
        float a = Mathf.Atan2(v.y, v.x)*Mathf.Rad2Deg;
        int oct = Mathf.RoundToInt(((a+360f)%360f)/45f)%8;    // 0=E,1=NE,...
        return new[]{2,3,4,5,6,7,0,1}[oct];                   // → our row order (0=S)
    }
    public void StartAction(Action a){ current=a; _frame=0; _frameT=0; }
    public void StopAction(){ current=Action.None; _frame=0; }
}
