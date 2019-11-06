using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[RequireComponent(typeof(LineRenderer))]
public class DottedLineMaker : MonoBehaviour {

    #region Public Fields
    public event Action OnTouchUp;
    static public DottedLineMaker instance;
    public Vector3[] linePath;
    public Vector3 initialPosition;
    
    public static float RIGHT_BOUND;
    public static float LEFT_BOUND;
    public static float UPPER_BOUND;
    #endregion

    #region Private Fields
    [SerializeField] private GameObject _dotPrefab;
    private List<GameObject> dots = new List<GameObject>();
    private LineRenderer _line;
    [SerializeField] private LayerMask _layerMask;
    private Camera _camera;
    private Vector3 _currentWorldPosition = Vector3.zero;
    #endregion

    #region Constants

    #endregion


    #region MonoBehaviour Callbacks

    private void Reset()
    {
        _dotPrefab = Resources.Load<GameObject>("LineDot");
        GetComponent<LineRenderer>().SetPositions(new Vector3[] { Vector3.zero, Vector3.zero, Vector3.zero });
    }

    private void Awake()
    {
        if (instance != null)
            Destroy(gameObject);
        instance = this;
        if (!_dotPrefab)
            _dotPrefab = Resources.Load<GameObject>("LineDot");
        _line = GetComponent<LineRenderer>();
        _line.SetPositions(new Vector3[] { Vector3.zero, Vector3.zero, Vector3.zero });
        _camera = Camera.main;
        RIGHT_BOUND = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width, 0f, 0f)).x;
        LEFT_BOUND = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width, 0f, 0f)).x * -1f;
        UPPER_BOUND = Camera.main.ScreenToWorldPoint(new Vector3(0f, Screen.height, 0f)).y;
    }

    private void Update()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        if (Input.GetMouseButton(0))
            SetLinePositions(Aim(GetCurrentWorldPosition()));
        else if (Input.GetMouseButtonUp(0))
        {
            if (OnTouchUp != null)
                OnTouchUp();
        }

#elif UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount == 1)
        {
            SetLinePositions(Aim(GetCurrentWorldPosition()));
            if (Input.GetTouch(0).phase == TouchPhase.Ended)
            {
                if (OnTouchUp != null)
                OnTouchUp();
            }
        }
#endif
    }
    #endregion

    #region Public Methods
    public void SetLinePositions(Vector3[] newPositions)
    {
        linePath = newPositions;
        _line.SetPositions(linePath);
        CreateDots();
        SpreadDots();

    }

    public void ResetLine()
    {
        for (int i = 0; i < _line.positionCount; i++)
        {
            _line.SetPosition(i, initialPosition);
        }
        HideDots();
    }

    #endregion

    #region Private Methods
    private int CalculateDotAmount()
    {
        float dotScale = _dotPrefab.transform.localScale.x;
        int lineDistance = (int)(Vector3.Distance(_line.GetPosition(0), _line.GetPosition(1)) + Vector3.Distance(_line.GetPosition(1), _line.GetPosition(2)));
        int dotAmount = lineDistance * 4 / (int)dotScale;
        return dotAmount;
    }

    private void CreateDots()
    {
        int neededDotAmount = CalculateDotAmount();
        if (dots.Count < neededDotAmount)
        {
            Vector3 hidePos = new Vector3(0f, -10f, 0f);
            while (dots.Count < neededDotAmount)
            {
                GameObject go = Instantiate(_dotPrefab, hidePos, Quaternion.identity);
                dots.Add(go);
            }
        }
    }

    private void SpreadDots()
    {
        List<GameObject> displayedDots = new List<GameObject>();
        int neededDotAmount = CalculateDotAmount();
        int j = 0;
        for (int i = 0; i < neededDotAmount; i++)
        {
            Vector3 dotPos = _line.GetPosition(0) + _line.GetPosition(1).normalized * (float)i / 2;
            if (dotPos.y <= _line.GetPosition(2).y)
            {
                dots[i].SetActive(true);
                if (dotPos.y >= _line.GetPosition(1).y)
                    dotPos = _line.GetPosition(1) + (_line.GetPosition(2) - _line.GetPosition(1)).normalized * (float)j++ / 2;
                dots[i].transform.position = dotPos;
                if (i % 2 == 0)
                    dots[i].GetComponent<Animator>().Play("LineDotReverseAnimation");
                else
                    dots[i].GetComponent<Animator>().Play("LineDotAnimation");
                displayedDots.Add(dots[i]);
            }
        }
        Vector3 hidePos = new Vector3(0f, -10f, 0f);
        for (int i = 0; i < dots.Count; i++)
        {
            if (!displayedDots.Contains(dots[i]))
            {
                dots[i].SetActive(false);
                dots[i].transform.position = hidePos;
            }

        }
    }

    private void HideDots()
    {
        Vector3 hidePos = new Vector3(0f, -10f, 0f);
        for (int i = 0; i < dots.Count; i++)
        {
            dots[i].SetActive(false);
            dots[i].transform.position = hidePos;
        }
    }

    public Vector3 GetCurrentWorldPosition()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        _currentWorldPosition = _camera.ScreenToWorldPoint(Input.mousePosition);
#elif UNITY_ANDROID || UNITY_IOS
        _currentWorldPosition = _camera.ScreenToWorldPoint(Input.GetTouch(0).position);
#endif
        _currentWorldPosition.z = initialPosition.z;
        return _currentWorldPosition;
    }

    public Vector3[] Aim(Vector3 touchPos)
    {
        //calculating the second ray point
        Vector3 angleCalculationVector = new Vector3(touchPos.x, 0f, 0f);
        float angle = Vector3.SignedAngle(angleCalculationVector, touchPos, Vector3.forward);
        float cos = Mathf.Cos(Mathf.Deg2Rad * angle);
        float xBound = touchPos.x >= initialPosition.x ? RIGHT_BOUND : LEFT_BOUND;
        float hypotenuse = xBound / cos;
        float oppositeY = Mathf.Sqrt(Mathf.Pow(hypotenuse, 2) - Mathf.Pow(xBound, 2));

        Vector3 secondRayPoint = new Vector3(xBound, oppositeY, initialPosition.z);
        RaycastHit hit = new RaycastHit();
        if (Physics.Raycast(initialPosition, secondRayPoint, out hit, 10f, _layerMask))
        {
            secondRayPoint = hit.point;
            return new Vector3[] { initialPosition, secondRayPoint, secondRayPoint };
        }

        //calculating the third ray point
        Vector3 thirdRayPoint = secondRayPoint;

        //if didnt hit a bubble
        if (hit.point == Vector3.zero)
        {
            float reflectionAngle = xBound == RIGHT_BOUND ? 90f - angle : -1 * (90f + angle);
            float ceilY = UPPER_BOUND - secondRayPoint.y;
            float tan = Mathf.Tan(Mathf.Deg2Rad * reflectionAngle);
            float ceilX = xBound == RIGHT_BOUND ? xBound - Mathf.Abs(tan * ceilY) : xBound + Mathf.Abs(tan * ceilY);
            thirdRayPoint = new Vector3(ceilX, UPPER_BOUND, initialPosition.z);
            Ray ray = new Ray(secondRayPoint, thirdRayPoint - secondRayPoint);
            if (Physics.Raycast(ray.origin, ray.direction, out hit, 50f, _layerMask))
            {
                thirdRayPoint = hit.point;
            }
        }

        return new Vector3[] { initialPosition, secondRayPoint, thirdRayPoint };
    }
    #endregion
}
