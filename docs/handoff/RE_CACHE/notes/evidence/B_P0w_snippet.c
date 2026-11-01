// FUN_14224f010 @ 14224f010

/* WARNING: Globals starting with '_' overlap smaller symbols at the same address */

ulonglong FUN_14224f010(longlong param_1)

{
  float fVar1;
  byte bVar2;
  ulonglong uVar3;
  
  if (*(char *)(param_1 + 0x6c) == '\x02') {
    return 0;
  }
  bVar2 = *(byte *)(param_1 + 0x6d) & 2;
  if ((*(byte *)(param_1 + 0x6d) & 4) != 0) {
    if (bVar2 != 0) goto code_r0x00014224f09b;
    if (cRam000000014502deac == '\0') {
      return (ulonglong)_UNK_1437e9990;
    }
  }
  if ((bVar2 == 0) && (cRam000000014502deac == '\0')) {
    fVar1 = *(float *)(param_1 + 0x54);
    if (0.0 < fVar1) {
      return (ulonglong)(uint)(fVar1 * fVar1 * fVar1 * fVar1 * _UNK_1437e9974 + _UNK_143c844b4);
    }
    uVar3 = func_0x000143324beb(_UNK_1436dba7c);
    return uVar3;
  }
code_r0x00014224f09b:
  return (ulonglong)_UNK_1437e9930;
}

