using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MakeTrap.Multiplayer
{
    public class TrapSelectionHUD : MonoBehaviour
    {
        public Image legoCard, bananaCard;
        public TMP_Text hint;
        Canvas canvas;
        TrapPlacementAnimation placement;
        NetworkPlayer player;

        void Awake()
        {
            canvas = GetComponent<Canvas>();
            placement = GetComponentInParent<TrapPlacementAnimation>();
            player = GetComponentInParent<NetworkPlayer>();
            canvas.enabled = false;
        }

        void LateUpdate()
        {
            canvas.enabled = placement != null && placement.isActiveAndEnabled &&
                (player == null || (player.IsSpawned && player.IsOwner && !player.IsBot));
            if (!canvas.enabled) return;
            var idle = new Color(.055f, .075f, .1f, .9f);
            var selected = new Color(.16f, .39f, .42f, .95f);
            legoCard.color = placement.PreviewActive && placement.SelectedTrap == 0 ? selected : idle;
            bananaCard.color = placement.PreviewActive && placement.SelectedTrap == 1 ? selected : idle;
            hint.text = placement.IsPlacing ? "설치 중…" : placement.PreviewActive ? "좌클릭 설치 · Esc 취소" : "Q / E  함정 선택";
        }
    }
}
