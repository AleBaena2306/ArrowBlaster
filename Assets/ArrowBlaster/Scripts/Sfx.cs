using System.Collections.Generic;
using UnityEngine;

namespace ArrowBlaster
{
    // Sonidos y música (Kenney, CC0) cargados de Resources/Audio. Sfx.Play("coin") desde cualquier sitio.
    // Para cambiar un sonido basta con sustituir el .ogg del mismo nombre.
    public sealed class Sfx : MonoBehaviour
    {
        private static Sfx _i;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        private AudioSource[] _voices;
        private AudioSource _music;
        private int _next;

        public static void Init()
        {
            if (_i != null) return;
            var go = new GameObject("Sfx");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<Sfx>();
            _i._voices = new AudioSource[10];
            for (int v = 0; v < _i._voices.Length; v++)
            {
                _i._voices[v] = go.AddComponent<AudioSource>();
                _i._voices[v].playOnAwake = false;
            }
            _i._music = go.AddComponent<AudioSource>();
            _i._music.loop = true;
            _i._music.volume = 0.35f;
            _i._music.playOnAwake = false;
            var clip = _i.Clip("music");
            if (clip != null) { _i._music.clip = clip; _i._music.Play(); }
        }

        private AudioClip Clip(string name)
        {
            if (!_clips.TryGetValue(name, out var c)) { c = Resources.Load<AudioClip>("Audio/" + name); _clips[name] = c; }
            return c;
        }

        // variants: número de versiones name_0..name_{n-1}; se elige una al azar.
        // Un mismo sonido no se repite en menos de 0,04 s (evita saturar con muchas monedas a la vez).
        public static void Play(string name, float volume = 1f, float pitch = 1f, int variants = 0)
        {
            if (_i == null) return;
            if (_i._last.TryGetValue(name, out var t) && Time.unscaledTime - t < 0.04f) return;
            _i._last[name] = Time.unscaledTime;
            var clip = _i.Clip(variants > 0 ? name + "_" + Random.Range(0, variants) : name);
            if (clip == null) return;
            var src = _i._voices[_i._next];
            _i._next = (_i._next + 1) % _i._voices.Length;
            src.pitch = pitch * Random.Range(0.95f, 1.05f);
            src.PlayOneShot(clip, volume);
        }

        public static bool Muted
        {
            get => AudioListener.volume < 0.5f;
            set => AudioListener.volume = value ? 0f : 1f;
        }
    }
}
