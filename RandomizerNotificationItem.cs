using UnityEngine;

namespace SilksongLoadoutRandomizer
{
    public class RandomizerNotificationItem : ICollectableUIMsgItem
    {
        private Sprite _sprite;
        private string _name;

        public RandomizerNotificationItem(string name, Sprite sprite)
        {
            _name = name;
            _sprite = sprite;
        }

        public Sprite GetUIMsgSprite() => _sprite;
        public string GetUIMsgName() => _name;
        public float GetUIMsgIconScale() => 1f;
        public bool HasUpgradeIcon() => false;
        public UnityEngine.Object GetRepresentingObject() => null;
    }
}
