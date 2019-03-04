using System;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEngine.UIElements;
using UnityEditor.VFX;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;

using PositionType = UnityEngine.UIElements.Position;

namespace UnityEditor.VFX.UI
{
    class VFXComponentBoard : VFXBoard, IControlledElement<VFXViewController>, IVFXMovable, IVFXResizable
    {
        Button          m_AttachButton;
        Button          m_SelectButton;
        Label           m_ComponentPath;
        VisualElement   m_ComponentContainer;
        VisualElement   m_EventsContainer;

        Button          m_Stop;
        Button          m_Play;
        Button          m_Step;
        Button          m_Restart;
        Slider          m_PlayRateSlider;
        IntegerField    m_PlayRateField;
        Button          m_PlayRateMenu;

        Label           m_ParticleCount;

        public VFXComponentBoard(VFXView view):base(view, BoardPreferenceHelper.Board.componentBoard,defaultRect)
        {
            var tpl = Resources.Load<VisualTreeAsset>("uxml/VFXComponentBoard");

            tpl.CloneTree(contentContainer);

            contentContainer.AddStyleSheetPath("VFXComponentBoard");

            m_AttachButton = contentContainer.Query<Button>("attach");
            m_AttachButton.clickable.clicked += ToggleAttach;

            m_SelectButton = contentContainer.Query<Button>("select");
            m_SelectButton.clickable.clicked += Select;

            m_ComponentPath = contentContainer.Query<Label>("component-path");

            m_ComponentContainer = contentContainer.Query("component-container");
            m_ComponentContainerParent = m_ComponentContainer.parent;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachToPanel);

            m_Stop = contentContainer.Query<Button>("stop");
            m_Stop.clickable.clicked += EffectStop;
            m_Play = contentContainer.Query<Button>("play");
            m_Play.clickable.clicked += EffectPlay;
            m_Step = contentContainer.Query<Button>("step");
            m_Step.clickable.clicked += EffectStep;
            m_Restart = contentContainer.Query<Button>("restart");
            m_Restart.clickable.clicked += EffectRestart;

            m_PlayRateSlider = contentContainer.Query<Slider>("play-rate-slider");
            m_PlayRateSlider.lowValue = Mathf.Pow(VisualEffectControl.minSlider, 1 / VisualEffectControl.sliderPower);
            m_PlayRateSlider.highValue = Mathf.Pow(VisualEffectControl.maxSlider, 1 / VisualEffectControl.sliderPower);
            m_PlayRateSlider.RegisterValueChangedCallback(evt => OnEffectSlider(evt.newValue));
            m_PlayRateField = contentContainer.Query<IntegerField>("play-rate-field");
            m_PlayRateField.RegisterCallback<ChangeEvent<int>>(OnPlayRateField);

            m_PlayRateMenu = contentContainer.Query<Button>("play-rate-menu");
            m_PlayRateMenu.AddStyleSheetPathWithSkinVariant("VFXControls");

            m_PlayRateMenu.clickable.clicked += OnPlayRateMenu;

            m_ParticleCount = contentContainer.Query<Label>("particle-count");

            Button button = contentContainer.Query<Button>("on-play-button");
            button.clickable.clicked += () => SendEvent("OnPlay");
            button = contentContainer.Query<Button>("on-stop-button");
            button.clickable.clicked += () => SendEvent("OnStop");

            m_EventsContainer = contentContainer.Query("events-container");

            Detach();
        }

        VisualElement m_ComponentContainerParent;

        static readonly Rect defaultRect = new Rect(200, 100, 300, 300);

        void OnMouseClick(MouseDownEvent e)
        {
            m_View.SetBoardToFront(this);
        }

        void OnPlayRateMenu()
        {
            GenericMenu menu = new GenericMenu();
            foreach (var value in VisualEffectControl.setPlaybackValues)
            {
                menu.AddItem(EditorGUIUtility.TextContent(string.Format("{0}%", value)), false, SetPlayRate, value);
            }
            menu.DropDown(m_PlayRateMenu.worldBound);
        }

        void OnPlayRateField(ChangeEvent<int> e)
        {
            SetPlayRate(e.newValue);
        }

        void SetPlayRate(object value)
        {
            if (m_AttachedComponent == null)
                return;
            float rate = (float)((int)value) * VisualEffectControl.valueToPlayRate;
            m_AttachedComponent.playRate = rate;
            UpdatePlayRate();
        }

        void OnEffectSlider(float f)
        {
            if (m_AttachedComponent != null)
            {
                m_AttachedComponent.playRate = VisualEffectControl.valueToPlayRate * Mathf.Pow(f, VisualEffectControl.sliderPower);
                UpdatePlayRate();
            }
        }

        void EffectStop()
        {
            if (m_AttachedComponent != null)
                m_AttachedComponent.ControlStop();
        }

        void EffectPlay()
        {
            if (m_AttachedComponent != null)
                m_AttachedComponent.ControlPlayPause();
        }

        void EffectStep()
        {
            if (m_AttachedComponent != null)
                m_AttachedComponent.ControlStep();
        }

        void EffectRestart()
        {
            if (m_AttachedComponent != null)
                m_AttachedComponent.ControlRestart();
        }

        void OnAttachToPanel(AttachToPanelEvent e)
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        void OnDetachToPanel(DetachFromPanelEvent e)
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        VisualEffect m_SelectionCandidate;

        VisualEffect m_AttachedComponent;

        public VisualEffect GetAttachedComponent()
        {
            return m_AttachedComponent;
        }

        void OnSelectionChanged()
        {
            
            if (Selection.activeGameObject != null && controller != null)
            {
                VisualEffect newSelectionCandidate = null;
                newSelectionCandidate = Selection.activeGameObject.GetComponent<VisualEffect>();
                if (newSelectionCandidate != null && newSelectionCandidate.visualEffectAsset != controller.graph.visualEffectResource.asset)
                {
                    newSelectionCandidate = null;
                }
                if (newSelectionCandidate != null)
                    m_SelectionCandidate = newSelectionCandidate;
            }

            UpdateAttachButton();
        }

        bool m_LastKnownPauseState;
        void UpdatePlayButton()
        {
            if (m_AttachedComponent == null)
                return;

            if (m_LastKnownPauseState != m_AttachedComponent.pause)
            {
                m_LastKnownPauseState = m_AttachedComponent.pause;
                if (m_LastKnownPauseState)
                {
                    m_Play.AddToClassList("paused");
                }
                else
                {
                    m_Play.RemoveFromClassList("paused");
                }
            }
        }

        void UpdateAttachButton()
        {
            m_AttachButton.SetEnabled(m_SelectionCandidate != null || m_AttachedComponent != null && controller != null);

            m_AttachButton.text = m_AttachedComponent != null ? "Detach" : "Attach";
        }

        void Detach()
        {
            if (m_AttachedComponent != null)
            {
                m_AttachedComponent.playRate = 1;
                m_AttachedComponent.pause = false;
            }
            m_AttachedComponent = null;
            if (m_UpdateItem != null)
            {
                m_UpdateItem.Pause();
            }
            m_ComponentContainer.RemoveFromHierarchy();
            m_ComponentPath.text = "";
            UpdateAttachButton();
            if (m_EventsContainer != null)
                m_EventsContainer.Clear();
            m_Events.Clear();
            m_SelectButton.visible = false;
        }

        public void Attach(VisualEffect effect = null)
        {
            VisualEffect target = effect != null ? effect : m_SelectionCandidate;
            if (target != null)
            {
                m_AttachedComponent = target;
                UpdateAttachButton();
                m_LastKnownPauseState = !m_AttachedComponent.pause;
                UpdatePlayButton();

                if (m_UpdateItem == null)
                    m_UpdateItem = schedule.Execute(Update).Every(100);
                else
                    m_UpdateItem.Resume();
                if (m_ComponentContainer.parent == null)
                    m_ComponentContainerParent.Add(m_ComponentContainer);
                UpdateEventList();
                m_SelectButton.visible = true;
            }
        }

        public void SendEvent(string name)
        {
            if (m_AttachedComponent != null)
            {
                m_AttachedComponent.SendEvent(name);
            }
        }

        IVisualElementScheduledItem m_UpdateItem;


        float m_LastKnownPlayRate = -1;


        int m_LastKnownParticleCount = -1;

        void Update()
        {
            if (m_AttachedComponent == null || controller == null)
            {
                Detach();
                return;
            }

            string path = m_AttachedComponent.name;

            UnityEngine.Transform current = m_AttachedComponent.transform.parent;
            while (current != null)
            {
                path = current.name + " > " + path;
                current = current.parent;
            }

            if (EditorSceneManager.loadedSceneCount > 1)
            {
                path = m_AttachedComponent.gameObject.scene.name + " : " + path;
            }

            if (m_ComponentPath.text != path)
                m_ComponentPath.text = path;

            if (m_ParticleCount != null)
            {
                int newParticleCount = 0;//m_AttachedComponent.aliveParticleCount
                if (m_LastKnownParticleCount != newParticleCount)
                {
                    m_LastKnownParticleCount = newParticleCount;
                    m_ParticleCount.text = m_LastKnownParticleCount.ToString();
                }
            }

            UpdatePlayRate();
            UpdatePlayButton();
        }

        void UpdatePlayRate()
        {
            if (m_LastKnownPlayRate != m_AttachedComponent.playRate)
            {
                m_LastKnownPlayRate = m_AttachedComponent.playRate;
                float playRateValue = m_AttachedComponent.playRate * VisualEffectControl.playRateToValue;
                m_PlayRateSlider.value = Mathf.Pow(playRateValue, 1 / VisualEffectControl.sliderPower);
                if (m_PlayRateField != null && !m_PlayRateField.HasFocus())
                    m_PlayRateField.value = Mathf.RoundToInt(playRateValue);
            }
        }

        void ToggleAttach()
        {
            if (!object.ReferenceEquals(m_AttachedComponent, null))
            {
                Detach();
            }
            else
            {
                Attach();
            }
        }

        void Select()
        {
            if (m_AttachedComponent != null)
            {
                Selection.activeObject = m_AttachedComponent;
            }
        }

        public new void Clear()
        {
            Detach();
        }

        void IControlledElement.OnControllerChanged(ref ControllerChangedEvent e)
        {
            UpdateEventList();
        }

        static readonly string[] staticEventNames = new string[] {"OnPlay", "OnStop" };

        public void UpdateEventList()
        {
            if (m_AttachedComponent == null)
            {
                if (m_EventsContainer != null)
                    m_EventsContainer.Clear();
                m_Events.Clear();
            }
            else
            {
                var eventNames = controller.contexts.Select(t => t.model).OfType<VFXBasicEvent>().Select(t => t.eventName).Except(staticEventNames).Distinct().OrderBy(t => t).ToArray();

                foreach (var removed in m_Events.Keys.Except(eventNames).ToArray())
                {
                    var ui = m_Events[removed];
                    m_EventsContainer.Remove(ui);
                    m_Events.Remove(removed);
                }

                foreach (var added in eventNames.Except(m_Events.Keys).ToArray())
                {
                    var tpl = Resources.Load<VisualTreeAsset>("uxml/VFXComponentBoard-event");

                    tpl.CloneTree(m_EventsContainer);

                    VFXComponentBoardEventUI newUI = m_EventsContainer.Children().Last() as VFXComponentBoardEventUI;
                    if (newUI != null)
                    {
                        newUI.Setup();
                        newUI.name = added;
                        m_Events.Add(added, newUI);
                    }
                }

                if (!m_Events.Values.Any(t => t.nameHasFocus))
                {
                    SortEventList();
                }
            }
        }

        void SortEventList()
        {
            var eventNames = m_Events.Keys.OrderBy(t => t);
            //Sort events
            VFXComponentBoardEventUI prev = null;
            foreach (var eventName in eventNames)
            {
                VFXComponentBoardEventUI current = m_Events[eventName];
                if (current != null)
                {
                    if (prev == null)
                    {
                        current.SendToBack();
                    }
                    else
                    {
                        current.PlaceInFront(prev);
                    }
                    prev = current;
                }
            }
        }

        Dictionary<string, VFXComponentBoardEventUI> m_Events = new Dictionary<string, VFXComponentBoardEventUI>();

    }
    public class VFXComponentBoardEventUIFactory : UxmlFactory<VFXComponentBoardEventUI>
    {}
    public class VFXComponentBoardEventUI : VisualElement
    {
        public VFXComponentBoardEventUI()
        {
        }

        public void Setup()
        {
            m_EventName = this.Query<TextField>("event-name");
            m_EventName.isDelayed = true;
            m_EventName.RegisterCallback<ChangeEvent<string>>(OnChangeName);
            m_EventSend = this.Query<Button>("event-send");
            m_EventSend.clickable.clicked += OnSend;
        }

        void OnChangeName(ChangeEvent<string> e)
        {
            var board = GetFirstAncestorOfType<VFXComponentBoard>();
            if (board != null)
            {
                board.controller.ChangeEventName(m_Name, e.newValue);
            }
        }

        public bool nameHasFocus
        {
            get { return m_EventName.HasFocus(); }
        }

        public new string name
        {
            get
            {
                return m_Name;
            }

            set
            {
                m_Name = value;
                if (m_EventName != null)
                {
                    if (!m_EventName.HasFocus())
                        m_EventName.SetValueWithoutNotify(m_Name);
                }
            }
        }

        string      m_Name;
        TextField   m_EventName;
        Button      m_EventSend;

        void OnSend()
        {
            var board = GetFirstAncestorOfType<VFXComponentBoard>();
            if (board != null)
            {
                board.SendEvent(m_Name);
            }
        }
    }
}
