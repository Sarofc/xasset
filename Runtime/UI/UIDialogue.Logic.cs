using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Saro.UI
{
    public partial class UIDialogue : IEnumerator
    {

        public bool isOk { get; private set; }

        private bool _visible = true;

        #region IEnumerator implementation

        bool IEnumerator.MoveNext()
        {
            return _visible;
        }

        void IEnumerator.Reset()
        {
        }

        object IEnumerator.Current
        {
            get { return null; }
        }

        #endregion


        private static UIDialogue _prefab;
        private static readonly List<UIDialogue> _showed = new List<UIDialogue>();
        private static readonly List<UIDialogue> _hidden = new List<UIDialogue>();

        public static void Dispose()
        {
            foreach (var item in _hidden)
            {
                item.Destroy();
            }

            _hidden.Clear();

            foreach (var item in _showed)
            {
                item.Destroy();
            }

            _showed.Clear();
        }

        public static void CloseAll()
        {
            for (var index = 0; index < _showed.Count; index++)
            {
                var messageBox = _showed[index];
                messageBox.Hide();
                _hidden.Add(messageBox);
            }
            _showed.Clear();
        }

        public static UIDialogue Show(string title, string content, string ok = "确定", string no = "取消")
        {
            if (_hidden.Count > 0)
            {
                var mb = _hidden[0];
                mb.Init(title, content, ok, no);
                mb.gameObject.SetActive(true);
                _hidden.RemoveAt(0);
                return mb;
            }
            else
            {
                return Create(title, content, ok, no);
            }
        }

        private void Destroy()
        {
            text_dialogue_title = null;
            text_dialogue_content = null;
            text_dialogue_btnleft = null;
            text_dialogue_btnright = null;
            Object.Destroy(gameObject);
        }

        private static UIDialogue Create(string title, string content, string ok, string no)
        {
            if (_prefab == null)
                _prefab = Resources.Load<UIDialogue>("UIDialogue");

            var canvas = GameObject.FindObjectOfType<Canvas>();

            var ui = Object.Instantiate(_prefab, canvas.transform);

            ui.Init(title, content, ok, no);

            return ui;
        }

        private void Init(string title, string content, string ok, string no)
        {
            text_dialogue_title.text = title;
            text_dialogue_content.text = content;
            text_dialogue_btnleft.text = ok;
            text_dialogue_btnright.text = no;

            button_button_left.onClick.AddListener(OnClickOk);
            button_button_right.onClick.AddListener(OnClickNo);

            _showed.Add(this);
            _visible = true;
            isOk = false;
        }

        public enum EventId
        {
            Ok,
            No,
        }

        public Action<EventId> onComplete { get; set; }

        private void OnClickNo()
        {
            HandleEvent(EventId.No);
        }

        private void OnClickOk()
        {
            HandleEvent(EventId.Ok);
        }

        private void HandleEvent(EventId id)
        {
            switch (id)
            {
                case EventId.Ok:
                    break;
                case EventId.No:
                    break;
                default:
                    throw new ArgumentOutOfRangeException("id", id, null);
            }

            Close();

            isOk = id == EventId.Ok;

            if (onComplete == null) return;
            onComplete(id);
            onComplete = null;
        }

        public void Close()
        {
            Hide();
            _hidden.Add(this);
            _showed.Remove(this);
        }

        private void Hide()
        {
            gameObject.SetActive(false);
            _visible = false;
        }
    }
}