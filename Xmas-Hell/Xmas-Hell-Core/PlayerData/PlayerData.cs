﻿using XmasHell.Entities.Bosses;
using System;

#if ANDROID
using Android.Util;
using Android.Preferences;
using Android.Content;
#endif

namespace XmasHell.PlayerData
{
    public class PlayerData
    {
#if ANDROID
        private ISharedPreferences _preferences;
        private ISharedPreferencesEditor _preferencesEditor;

        public PlayerData(ISharedPreferences preferences)
        {
            _preferences = preferences;
            _preferencesEditor = preferences.Edit();
#else
        public PlayerData()
        {
#endif
        }

        public void DeathCounter(int value)
        {
#if ANDROID
            _preferencesEditor.PutInt("deathCounter", value);
            _preferencesEditor.Apply();
#endif
        }

        public int DeathCounter()
        {
#if ANDROID
            return _preferences.GetInt("deathCounter", 0);
#else
            return 0;
#endif
        }

        // Boss specific data

        public void BossBeatenCounter(BossType type, int value)
        {
#if ANDROID
            _preferencesEditor.PutInt("BossBeatenCounter-" + type.ToString(), value);
            _preferencesEditor.Apply();
#endif
        }

        public int BossBeatenCounter(BossType type)
        {
#if ANDROID
            return _preferences.GetInt("BossBeatenCounter-" + type.ToString(), 0);
#else
            return 0;
#endif
        }

        public void BossAttempts(BossType type, int value)
        {
#if ANDROID
            _preferencesEditor.PutInt("BossAttempts-" + type.ToString(), value);
            _preferencesEditor.Apply();
#endif
        }

        public int BossAttempts(BossType type)
        {
#if ANDROID
            return _preferences.GetInt("BossAttempts-" + type.ToString(), 0);
#else
            return 0;
#endif
        }

        public void BossBestTime(BossType type, TimeSpan value)
        {
#if ANDROID
            _preferencesEditor.PutString("BossBestTime-" + type.ToString(), value.TotalSeconds.ToString());
            _preferencesEditor.Apply();
#endif
        }

        public TimeSpan BossBestTime(BossType type)
        {
#if ANDROID
            return TimeSpan.FromSeconds(double.Parse(_preferences.GetString("BossBestTime-" + type.ToString(), "0")));
#else
            return TimeSpan.Zero;
#endif
        }

        public void BossPlayTime(BossType type, TimeSpan value)
        {
#if ANDROID
            _preferencesEditor.PutString("BossPlayTime-" + type.ToString(), value.TotalSeconds.ToString());
            _preferencesEditor.Apply();
#endif
        }

        public TimeSpan BossPlayTime(BossType type)
        {
#if ANDROID
            return TimeSpan.FromSeconds(double.Parse(_preferences.GetString("BossPlayTime-" + type.ToString(), "0")));
#else
            return TimeSpan.Zero;
#endif
        }
    }
}
