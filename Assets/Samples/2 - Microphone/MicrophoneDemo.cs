using System.Collections;
using System.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Whisper.Utils;
using Button = UnityEngine.UI.Button;
using Toggle = UnityEngine.UI.Toggle;

namespace Whisper.Samples
{
    /// <summary>
    /// Record audio clip from microphone and make a transcription.
    /// </summary>
    public class MicrophoneDemo : MonoBehaviour
    {
        public WhisperManager whisper;
        public MicrophoneRecord microphoneRecord;
        public bool streamSegments = true;
        public bool printLanguage = true;

        [Header("UI")] 
        public Button button;
        public Text buttonText;
        public Text outputText;
        public Text timeText;
        public Dropdown languageDropdown;
        public Toggle translateToggle;
        public Toggle vadToggle;
        public ScrollRect scroll;

        [Header("Signs")]
        [SerializeField] private GameObject Arrow_sign;
        [SerializeField] private GameObject Stop_sign;

        [Header("3D Text")]
        public TMP_Text text3D; // Ссылка на 3D текст
        public float text3DDuration = 5f; // Время отображения 3D текста в секундах
        public bool autoHideText3D = true; // Автоматически скрывать 3D текст
        private float _text3DTimer;

        [Header("ESP8266")]
        public string esp8266IpAddress = "10.149.186.175"; // IP вашего ESP8266

        private string _buffer;

        private void Awake()
        {
            whisper.OnNewSegment += OnNewSegment;
            whisper.OnProgress += OnProgressHandler;
            
            microphoneRecord.OnRecordStop += OnRecordStop;
            
            button.onClick.AddListener(OnButtonPressed);
            languageDropdown.value = languageDropdown.options
                .FindIndex(op => op.text == whisper.language);
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);

            translateToggle.isOn = whisper.translateToEnglish;
            translateToggle.onValueChanged.AddListener(OnTranslateChanged);

            vadToggle.isOn = microphoneRecord.vadStop;
            vadToggle.onValueChanged.AddListener(OnVadChanged);

            if (text3D != null)
            {
                text3D.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (!microphoneRecord.IsRecording)
            {
                microphoneRecord.StartRecord();
                buttonText.text = "Stop";
            }
        }

        private void Update()
        {
            // Автоматическое скрытие 3D текста через заданное время
            if (autoHideText3D && text3D != null && text3D.gameObject.activeSelf)
            {
                _text3DTimer -= Time.deltaTime;
                if (_text3DTimer <= 0)
                {
                    text3D.gameObject.SetActive(false);
                }
            }
        }

        private void OnVadChanged(bool vadStop)
        {
            microphoneRecord.vadStop = vadStop;
        }

        private void OnButtonPressed()
        {
            if (!microphoneRecord.IsRecording)
            {
                microphoneRecord.StartRecord();
                buttonText.text = "Stop";
            }
            else
            {
                microphoneRecord.StopRecord();
                buttonText.text = "Record";
            }
        }
        
        private async void OnRecordStop(AudioChunk recordedAudio)
        {
            buttonText.text = "Record";
            _buffer = "";

            var sw = new Stopwatch();
            sw.Start();
            
            var res = await whisper.GetTextAsync(recordedAudio.Data, recordedAudio.Frequency, recordedAudio.Channels);
            if (res == null || !outputText) 
                return;

            var time = sw.ElapsedMilliseconds;
            var rate = recordedAudio.Length / (time * 0.001f);
            timeText.text = $"Time: {time} ms\nRate: {rate:F1}x";

            var text = res.Result;
            if (printLanguage)
                text += $"\n\nLanguage: {res.Language}";
            
            outputText.text = text;
            UiUtils.ScrollDown(scroll);

            UpdateText3D(text);
            ProcessVoiceCommand(text);

            if (!microphoneRecord.IsRecording)
            {
                microphoneRecord.StartRecord();
                buttonText.text = "Processing...";
            }
        }
        
        private void OnLanguageChanged(int ind)
        {
            var opt = languageDropdown.options[ind];
            whisper.language = opt.text;
        }
        
        private void OnTranslateChanged(bool translate)
        {
            whisper.translateToEnglish = translate;
        }

        private void OnProgressHandler(int progress)
        {
            if (!timeText)
                return;
            timeText.text = $"Progress: {progress}%";
        }
        
        private void OnNewSegment(WhisperSegment segment)
        {
            if (!streamSegments || !outputText)
                return;

            _buffer += segment.Text;
            outputText.text = _buffer + "...";
            UiUtils.ScrollDown(scroll);
        }

        private void UpdateText3D(string text)
        {
            if (text3D == null)
            {
                return;
            }

            // Ограничиваем длину текста, если нужно
            string displayText = text;
            if (displayText.Length > 50) // Обрезаем до 50 символов для читаемости
            {
                displayText = displayText.Substring(0, 50) + "...";
            }

            // Устанавливаем текст
            text3D.text = displayText;

            // Показываем объект
            text3D.gameObject.SetActive(true); // Отображение текста после голоса text3D.gameObject.SetActive(true)

            // Сбрасываем таймер для автоматического скрытия
            if (autoHideText3D)
            {
                _text3DTimer = text3DDuration;
            }
        }

        private void ProcessVoiceCommand(string command)
        {
            command = command.ToLower().Trim();

            if (command.Contains("вперед") || command.Contains("вперёд.") || command.Contains("вперед!") ||
                command.Contains("вверх") || command.Contains("вверх.") || command.Contains("вверх!") ||
                command.Contains("верх") || command.Contains("верх.") || command.Contains("верх!") ||
                command.Contains("прямо") || command.Contains("прямо.") || command.Contains("прямо!"))
            {
                UnityEngine.Debug.Log("Executing: Move Forward");
                Stop_sign.SetActive(false);
                Arrow_sign.transform.localEulerAngles = new Vector3(0, 90, 270);
                Arrow_sign.SetActive(true);
                SendCommandToESP8266("/forward");
            }
            else if (command.Contains("назад") || command.Contains("назад.") || command.Contains("назад!") ||
                     command.Contains("вниз") || command.Contains("вниз.") || command.Contains("вниз!"))
            {
                UnityEngine.Debug.Log("Executing: Move Backward");
                Stop_sign.SetActive(false);
                Arrow_sign.transform.localEulerAngles = new Vector3(0, 90, 90);
                Arrow_sign.SetActive(true);
                SendCommandToESP8266("/backward");
            }
            else if (command.Contains("влево") || command.Contains("влево.") || command.Contains("влево!") || 
                     command.Contains("лево") || command.Contains("лево.") || command.Contains("лево!"))
            {
                UnityEngine.Debug.Log("Executing: Turn Left");
                Stop_sign.SetActive(false);
                Arrow_sign.transform.localEulerAngles = new Vector3(90, 0, 0);
                Arrow_sign.SetActive(true);
                SendCommandToESP8266("/left");
            }
            else if (command.Contains("вправо") || command.Contains("вправо.") || command.Contains("вправо!") || 
                     command.Contains("право") || command.Contains("право.") || command.Contains("право!"))
            {
                UnityEngine.Debug.Log("Executing: Turn Right");
                Stop_sign.SetActive(false);
                Arrow_sign.transform.localEulerAngles = new Vector3(90, 0, 180);
                Arrow_sign.SetActive(true);
                SendCommandToESP8266("/right");
            }
            else if (command.Contains("стоп") || command.Contains("стоп.") || command.Contains("стоп!") ||
                     command.Contains("остановись") || command.Contains("остановись.") || command.Contains("остановись!") || 
                     command.Contains("стой") || command.Contains("стой.") || command.Contains("стой!"))
            {
                UnityEngine.Debug.Log("Executing: Stop");
                Arrow_sign.SetActive(false);
                Stop_sign.SetActive(true);
                SendCommandToESP8266("/stop");
            }
            else if (command.Contains("разворот") || command.Contains("развернись") || command.Contains("кругом"))
            {
                UnityEngine.Debug.Log("Executing: Turn Around");
                StartCoroutine(TurnAround());
            }
        }

        private IEnumerator TurnAround()
        {
            SendCommandToESP8266("/left");
            yield return new WaitForSeconds(0.5f);
            SendCommandToESP8266("/left");
            yield return new WaitForSeconds(0.5f);
            SendCommandToESP8266("/stop");
        }

        public void SendCommandToESP8266(string command)
        {
            StartCoroutine(SendCommandCoroutine(command));
        }

        private IEnumerator SendCommandCoroutine(string command)
        {
            string url = $"http://{esp8266IpAddress}{command}";
            UnityEngine.Debug.Log($"Sending command to: {url}");

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                www.timeout = 3;
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    UnityEngine.Debug.Log($"Command {command} sent successfully. Response: {www.downloadHandler.text}");
                }
                else
                {
                    UnityEngine.Debug.LogError($"Failed to send command: {www.error}");
                    UnityEngine.Debug.LogError($"URL: {url}");
                    UnityEngine.Debug.LogError($"Response Code: {www.responseCode}");

                    yield return new WaitForSeconds(0.5f);
                    StartCoroutine(SendCommandCoroutine(command));
                }
            }
        }

        private void OnApplicationQuit()
        {
            SendCommandToESP8266("/stop");
        }
    }
}