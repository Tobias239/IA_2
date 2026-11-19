using UnityEngine;

[RequireComponent(typeof(FOVSensor))]
public class FOVVisualFeedback : MonoBehaviour
{
    [SerializeField] private Renderer _bodyRenderer;
    [SerializeField] private Color _alertColor = Color.red;

    private FOVSensor _sensor;
    private Material _matInstance;
    private Color _defaultColor;

    private void Awake()
    {
        _sensor = GetComponent<FOVSensor>();
        if (_bodyRenderer == null) return;

        _matInstance = _bodyRenderer.material;
        _defaultColor = _matInstance.color;
    }

    private void Update()
    {
        if (_matInstance == null) return;

        _matInstance.color = _sensor.CanSeeTarget ? _alertColor : _defaultColor;
    }

    private void OnDestroy()
    {
        if (_matInstance != null) Destroy(_matInstance);
    }
}