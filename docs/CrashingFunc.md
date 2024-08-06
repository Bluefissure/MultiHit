# Crashing with massive flytexts

I guess it's caused by some weird edge case that frequent flytext triggering refreshed the memory to some invalid zero pointer,
dig into the function and found two functions:

the crashing one is

<details><summary>Inner Function</summary>

```
void __fastcall sub_14133C480(__int64 a1, __int64 a2, unsigned int a3, __int64 a4, char a5)
{
  float v5; // xmm3_4
  __int64 v6; // r12
  float v8; // xmm8_4
  __int16 *v9; // r15
  __int64 v10; // r14
  _QWORD *v11; // rax
  _QWORD *v12; // rbx
  _QWORD **v13; // r13
  __int64 *v14; // rdi
  __int64 v15; // rsi
  float v16; // xmm0_4
  float v17; // xmm1_4
  double v18; // xmm0_8
  float v19; // xmm7_4
  double v20; // xmm0_8
  float v21; // xmm6_4
  double v22; // xmm0_8
  float v23; // xmm7_4
  double v24; // xmm0_8
  float v25; // xmm7_4
  double v26; // xmm0_8
  float v27; // xmm6_4
  double v28; // xmm0_8
  float v29; // xmm7_4
  double v30; // xmm0_8
  float v31; // xmm6_4
  __int64 v32; // rdx
  float v33; // xmm6_4
  int v34; // eax
  _QWORD **v35; // rsi
  _QWORD *v36; // rax
  _QWORD *v37; // rcx
  int v38; // ecx
  float v39; // xmm0_4
  float *v40; // rax
  float v41; // xmm0_4
  _QWORD *v42; // rdi
  _QWORD *v43; // rbx
  __int64 v44; // rsi
  _QWORD *v45; // rax
  _QWORD *v46; // rcx
  __int64 v47; // [rsp+E0h] [rbp+8h]
  float v48; // [rsp+E8h] [rbp+10h] BYREF
  int v49; // [rsp+F0h] [rbp+18h] BYREF

  v6 = a1 + 11592;
  v8 = v5;
  v9 = 0i64;
  v10 = 6i64 * a3;
  v47 = (unsigned int)a2;
  sub_141379570(a1 + 11592);
  v11 = *(_QWORD **)(a1 + 8 * v10 + 11064);
  v12 = (_QWORD *)*v11;
  if ( v11 != (_QWORD *)*v11 )
  {
    do
    {
      v13 = (_QWORD **)(v11 + 1);
      v14 = *(__int64 **)(v11[1] + 16i64);
      v15 = *v14;                               // crashing here
      if ( a5 || *((__int16 *)v14 + 39) >= 0 )
      {
        v18 = Component::GUI::AtkResNode_GetYFloat(*v14);
        v17 = *(float *)&v18 + (float)(v8 + *(float *)(a1 + 8 * v10 + 11092));
      }
      else
      {
        if ( v8 >= 0.0 )
          v16 = 1.0;
        else
          v16 = -1.0;
        v17 = (float)((float)((float)*(int *)(a1 + 4 * v47 + 11544) * 0.5) * v16) + *(float *)(a1 + 8 * v10 + 11084);
      }
      Component::GUI::AtkResNode_SetYFloat(v15, v17);
      Component::GUI::AtkResNode_GetXFloat(v15);
      Component::GUI::AtkResNode_SetXFloat(v15);
      *((float *)v14 + 18) = (float)(v8 + *(float *)(a1 + 8 * v10 + 11092)) + *((float *)v14 + 18);
      if ( v9 && (a5 || v9[39] >= 0 && *((__int16 *)v14 + 39) >= 0) )
      {
        if ( v8 >= 0.0 )
        {
          v25 = (float)((unsigned __int16)sub_14063F820(*(_QWORD *)v9) - 11);
          v26 = Component::GUI::AtkResNode_GetYFloat(v15);
          v27 = *(float *)&v26;
          v28 = Component::GUI::AtkResNode_GetYFloat(*(_QWORD *)v9);
          v29 = v25 - (float)(v27 - *(float *)&v28);
          if ( v29 > 0.0 )
          {
            v8 = v8 + v29;
            v30 = Component::GUI::AtkResNode_GetYFloat(v15);
            Component::GUI::AtkResNode_SetYFloat(v15, *(float *)&v30 + v29);
            *((float *)v14 + 18) = v29 + *((float *)v14 + 18);
          }
        }
        else
        {
          v19 = (float)((unsigned __int16)sub_14063F820(v15) - 11);
          v20 = Component::GUI::AtkResNode_GetYFloat(*(_QWORD *)v9);
          v21 = *(float *)&v20;
          v22 = Component::GUI::AtkResNode_GetYFloat(v15);
          v23 = v19 - (float)(v21 - *(float *)&v22);
          if ( v23 > 0.0 )
          {
            v8 = v8 - v23;
            v24 = Component::GUI::AtkResNode_GetYFloat(v15);
            Component::GUI::AtkResNode_SetYFloat(v15, *(float *)&v24 - v23);
            *((float *)v14 + 18) = *((float *)v14 + 18) - v23;
          }
        }
      }
      v9 = (__int16 *)v14;
      v31 = *((float *)v14 + 18)
          - (float)((float)(Component::GUI::AtkUnitBase_GetGlobalUIScale()
                          * (float)(*(float *)(a1 + 11580) * *(float *)(a1 + 8 * v10 + 11096)))
                  + *(float *)(a1 + 8 * v10 + 11084));
      if ( v8 < 0.0 )
        v31 = v31 * -1.0;
      v33 = v31 / Component::GUI::AtkUnitBase_GetGlobalUIScale();
      v34 = *(_DWORD *)(a1 + 4 * v47 + 11544);
      if ( v33 < (float)v34 )
      {
        v38 = *(_DWORD *)(a1 + 11576);
        v39 = (float)(v34 - v38);
        if ( v33 < v39 )
        {
          LOBYTE(v32) = -1;
        }
        else
        {
          v49 = 1065353216;
          v40 = &v48;
          v48 = 1.0 - (float)((float)(v33 - v39) / (float)v38);
          if ( v48 >= 1.0 )
            v40 = (float *)&v49;
          v41 = *v40;
          if ( *v40 <= 0.0 )
            v41 = 0.0;
          v32 = (unsigned int)(int)(float)(v41 * 255.0);
        }
        Component::GUI::AtkResNode_SetAlpha(v15, v32);
      }
      else
      {
        v35 = *(_QWORD ***)v6;
        if ( *(_QWORD *)(v6 + 8) == 0xFFFFFFFFi64 )
          std::_Xlength_error("list too long");
        v36 = (_QWORD *)sub_140066600(24i64, 0i64, 400i64);
        v36[2] = v14;
        ++*(_QWORD *)(v6 + 8);
        v37 = v35[1];
        v9 = 0i64;
        *v36 = v35;
        v36[1] = v37;
        v35[1] = v36;
        *v37 = v36;
      }
      v11 = *v13;
    }
    while ( *v13 != v12 );
  }
  v42 = *(_QWORD **)v6;
  v43 = **(_QWORD ***)v6;
  if ( v43 != *(_QWORD **)v6 )
  {
    do
    {
      v44 = v43[2];
      if ( v44 )
      {
        *(_WORD *)(v44 + 78) &= ~0x2000u;
        Component::GUI::AtkResNode_ToggleVisibility(*(_QWORD *)v44, 0i64);
      }
      v45 = *(_QWORD **)(a1 + 8 * v10 + 11064);
      v46 = (_QWORD *)*v45;
      if ( (_QWORD *)*v45 != v45 )
      {
        while ( v46[2] != v44 )
        {
          v46 = (_QWORD *)*v46;
          if ( v46 == v45 )
            goto LABEL_40;
        }
        *(_QWORD *)v46[1] = *v46;
        *(_QWORD *)(*v46 + 8i64) = v46[1];
        --*(_QWORD *)(a1 + 8 * v10 + 11072);
        SpecialFreeMemory(v46, 24i64, 400i64);
      }
LABEL_40:
      v43 = (_QWORD *)*v43;
    }
    while ( v43 != v42 );
  }
}
```

(Sig:`E8 ? ? ? ? 8B E8 48 83 BB ? ? ? ? ? `)

</details>

after analyzing the crash.log, the v14(rdi) was 0 at the moment, causing the `v15=*v14` accessing a null pointer.

however, hooking this function completely turned off the flytext system, so let's hooking the upper level function:

<details><summary>Outer Function</summary>

```
void __fastcall sub_14133C8F0(__int64 a1, float a2)
{
  __int64 v4; // rax
  __int64 v5; // rdi
  __int64 v6; // rax
  __int64 v7; // rax
  __int64 v8; // rdx
  _DWORD *v9; // rdx
  float v10; // xmm2_4
  float v11; // xmm0_4
  float v12; // xmm1_4
  float v13; // xmm3_4
  float v14; // xmm2_4
  float v15; // xmm3_4
  float v16; // xmm2_4
  float v17; // xmm3_4
  float v18; // xmm2_4
  float v19; // xmm3_4
  float v20; // xmm2_4
  int v21; // ebp
  float v22; // xmm3_4
  float v23; // xmm2_4
  float v24; // xmm3_4
  float v25; // xmm2_4
  float v26; // xmm3_4
  float v27; // xmm1_4
  __int64 v28; // r9
  double v29; // xmm0_8
  int v30; // eax
  int v31; // eax
  _QWORD *v32; // rsi
  unsigned int i; // edi
  int v34; // eax
  int v35; // eax

  v4 = ((__int64 (*)(void))Component::GUI::AtkStage_Instance)();
  v5 = Component::GUI::AtkStage_GetNumberArrayData(v4);
  v6 = Component::GUI::AtkStage_Instance(a1);
  v7 = Component::GUI::AtkStage_GetStringArrayData(v6);
  if ( v5 )
  {
    if ( v7 )
    {
      v8 = *(_QWORD *)(v5 + 240);
      if ( v8 )
      {
        if ( *(_QWORD *)(v7 + 216) )
        {
          v9 = *(_DWORD **)(v8 + 32);
          v10 = (float)(unsigned __int16)v9[3];
          v11 = (float)((int)v9[3] >> 16) - *(float *)(a1 + 11176);
          *(float *)(a1 + 11176) = (float)((int)v9[3] >> 16);
          v12 = v10 - *(float *)(a1 + 11180);
          *(float *)(a1 + 11180) = v10;
          *(float *)(a1 + 11184) = v11;
          *(float *)(a1 + 11188) = v12;
          v13 = (float)((int)v9[4] >> 16);
          v14 = (float)(unsigned __int16)v9[4];
          *(float *)(a1 + 11232) = v13 - *(float *)(a1 + 11224);
          *(float *)(a1 + 11236) = v14 - *(float *)(a1 + 11228);
          *(float *)(a1 + 11224) = v13;
          *(float *)(a1 + 11228) = v14;
          v15 = (float)((int)v9[5] >> 16);
          v16 = (float)(unsigned __int16)v9[5];
          *(float *)(a1 + 11280) = v15 - *(float *)(a1 + 11272);
          *(float *)(a1 + 11284) = v16 - *(float *)(a1 + 11276);
          *(float *)(a1 + 11272) = v15;
          *(float *)(a1 + 11276) = v16;
          v17 = (float)((int)v9[6] >> 16);
          v18 = (float)(unsigned __int16)v9[6];
          *(float *)(a1 + 11328) = v17 - *(float *)(a1 + 11320);
          *(float *)(a1 + 11332) = v18 - *(float *)(a1 + 11324);
          *(float *)(a1 + 11320) = v17;
          *(float *)(a1 + 11324) = v18;
          v19 = (float)((int)v9[7] >> 16);
          v20 = (float)(unsigned __int16)v9[7];
          *(float *)(a1 + 11376) = v19 - *(float *)(a1 + 11368);
          *(float *)(a1 + 11380) = v20 - *(float *)(a1 + 11372);
          *(float *)(a1 + 11368) = v19;
          v21 = 0;
          *(float *)(a1 + 11372) = v20;
          v22 = (float)((int)v9[8] >> 16);
          v23 = (float)(unsigned __int16)v9[8];
          *(float *)(a1 + 11424) = v22 - *(float *)(a1 + 11416);
          *(float *)(a1 + 11428) = v23 - *(float *)(a1 + 11420);
          *(float *)(a1 + 11416) = v22;
          *(float *)(a1 + 11420) = v23;
          v24 = (float)((int)v9[9] >> 16);
          v25 = (float)(unsigned __int16)v9[9];
          *(float *)(a1 + 11472) = v24 - *(float *)(a1 + 11464);
          *(float *)(a1 + 11476) = v25 - *(float *)(a1 + 11468);
          *(float *)(a1 + 11464) = v24;
          *(float *)(a1 + 11468) = v25;
          v26 = (float)((int)v9[10] >> 16);
          v27 = (float)(unsigned __int16)v9[10];
          *(float *)(a1 + 11520) = v26 - *(float *)(a1 + 11512);
          *(float *)(a1 + 11524) = v27 - *(float *)(a1 + 11516);
          *(float *)(a1 + 11512) = v26;
          *(float *)(a1 + 11516) = v27;
          v29 = Component::GUI::AtkUnitBase_GetGlobalUIScale();
          if ( (float)(*(float *)&v29 * (float)(a2 * *(float *)(a1 + 11560))) > 0.0 )
          {
            if ( *(_QWORD *)(a1 + 11072) )
            {
              sub_14133C480(a1, 0i64, 0, v28, 1);
              v21 = v30;
            }
            if ( *(_QWORD *)(a1 + 11120) )
            {
              sub_14133C480(a1, 1i64, 1u, v28, 1);
              v21 += v31;
            }
            v32 = (_QWORD *)(a1 + 11168);
            for ( i = 2; i < 0xA; ++i )
            {
              if ( *v32 )
              {
                sub_14133C480(a1, 2i64, i, v28, 0);
                v21 += v34;
              }
              v32 += 6;
            }
            v35 = (*(_DWORD *)(a1 + 400) & 0xF00000) - 0x400000;
            if ( v21 )
            {
              if ( (v35 & 0xFFEFFFFF) == 0 && *(char *)(a1 + 434) <= 0 )
                (*(void (__fastcall **)(__int64, _QWORD, __int64))(*(_QWORD *)a1 + 40i64))(a1, 0i64, 15i64);
            }
            else if ( (v35 & 0xFFEFFFFF) != 0 )
            {
              (*(void (__fastcall **)(__int64, _QWORD, _QWORD, __int64))(*(_QWORD *)a1 + 48i64))(a1, 0i64, 0i64, 1i64);
            }
          }
        }
      }
    }
  }
}
```

(Sig:`E8 ? ? ? ? F3 0F 58 B6 ? ? ? ? 0F 2F 35 ? ? ? ? `)

</details>

It looks like calling `sub_14133C480` to at most 10 objects, so let's validate the `v15=*v14` before the original hook.
