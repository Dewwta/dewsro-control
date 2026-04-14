#pragma once

#include "ClassLink.h"
#include "Data/CharacterData.h"
#include "IGIDObject.h"
#include <BSLib/support/AbiTest.h>
#include <BSLib/support/undefined.h>

enum TRIJOB_TYPE : char { /* Job Type of the Player */
    TRIJOB_HUNTER = 3,
    TRIJOB_NOJOB = 4,
    TRIJOB_THIEF = 2,
    TRIJOB_TRADER = 1
};

enum eAbnormalState : unsigned int {
    ABNORMAL_STATE_FROZEN = 1,
    ABNORMAL_STATE_FROSTBITTEN = 2,
    ABNORMAL_STATE_BURN = 8,
    ABNORMAL_STATE_ZOMBIE = 0x20
};

class CICharactor : public CIGIDObject {
    friend CClassLink<CICharactor>;
public:
    union uCharacterState {
        unsigned short state;
        struct {
            unsigned char stateH;
            unsigned char stateL;
        };
    };

    /// \address 009cf1b0
    void Func_15(int param_1, float *param_2) override;
    void Func_15_impl(int param_1, float *param_2);

    virtual void Func_34();

    virtual void Func_35();

    virtual const SCommonData &Func_36(); // TODO:: could lead to crashes further on

    virtual void Func_37();

    virtual void Func_38();

    virtual void SetMaxHP();

    virtual void Func_40();

    virtual void Func_41();

    virtual void Func_42();

    virtual TRIJOB_TYPE GetJobType();

    virtual void SetJobType(TRIJOB_TYPE type);

    virtual void Func_45();

    virtual void Func_46();

    virtual void Func_47();

    virtual void Func_48();

    virtual void Func_49();

    virtual void Func_50();

    virtual void Func_51();

    virtual void Func_52();

    virtual void Func_53();

    virtual void Func_54();

public:
    unsigned int GetCurrentHp() const;

    unsigned int GetMaxHp() const;

    unsigned int GetCurrentMp() const;

    unsigned int GetMaxMp() const;

    /// \address 009c7880
    unsigned int FUN_009c7880() const;

private:
    char pad_0254[32]; //0x0254
    class CClassLink<CICharactor> classLink; //0x0274
    class std::list<void*> decolist; //0x0284
    char pad_0290[0x02C4 - 0x0290]; //0x0290
public:
    eAbnormalState m_abnormalState; // 0x02C4
private:
    char pad_02c8[0x0454 - 0x02c8];
    unsigned int m_mpCurrent; //0x0454
    unsigned int m_hpMax; //0x0458
    unsigned int m_mpMax; //0x045C
    unsigned int m_hpCurrent; //0x0460
    char pad_0464[4]; //0x0464
    unsigned int N00000A38; //0x0468
    char pad_046C[8]; //0x046C
protected:
    class CGFontTexture m_textureMessageAbove; //0x0474
    D3DCOLOR m_colorMessageAbove; //0x04DC
private:
    char pad_04e0[16]; //0x04E0
protected:
    float m_renderY; //0x04F0
    float m_renderX; //0x04F4
    float m_renderDepth; //0x04F8
private:
    char pad_04FC[17]; //0x04FC
    TRIJOB_TYPE b_JobType; //0x050D
    char pad_050E[0x065C - 0x050E]; //0x050E
public:
    uCharacterState m_characterState; // 0x065C
private:
    char pad_0x660[0x78c - 0x0660];
private:
    BEGIN_FIXTURE()
        ENSURE_SIZE(0x78c)
        ENSURE_OFFSET(classLink, 0x274)
        ENSURE_OFFSET(decolist, 0x284)
        ENSURE_OFFSET(m_abnormalState, 0x02C4)
        ENSURE_OFFSET(m_mpCurrent, 0x0454)
        ENSURE_OFFSET(m_hpMax, 0x0458)
        ENSURE_OFFSET(m_mpMax, 0x045C)
        ENSURE_OFFSET(m_hpCurrent, 0x0460)
        ENSURE_OFFSET(m_textureMessageAbove, 0x474)
        ENSURE_OFFSET(m_colorMessageAbove, 0x4DC)
        ENSURE_OFFSET(m_renderY, 0x4F0)
        ENSURE_OFFSET(m_renderX, 0x4F4)
        ENSURE_OFFSET(m_renderDepth, 0x4F8)
        ENSURE_OFFSET(b_JobType, 0x50d)
        ENSURE_OFFSET(m_characterState, 0x65c)
    END_FIXTURE()

    RUN_FIXTURE(CICharactor)
};
